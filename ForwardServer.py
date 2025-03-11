"""
A TCP server that forwards messages between Unity client and OpenAI API server.

"""

import socket
import threading
from openai import OpenAI
from dotenv import load_dotenv
from tokenCount import num_tokens_from_messages, num_tokens_from_response

import sys, os
import base64
import re

import time

current_file_path = os.path.abspath(__file__)
current_directory = os.path.dirname(current_file_path)
# load the .env file containing your API key
load_dotenv(dotenv_path=current_directory + "/.env")

# receive messege HEAD format
HEAD_LEN = 10
# first byte indicates the role of the message, 0 for user (delete later), 1 for assistant, 2 for system, 3 for user as example
# the following bytes indicate the length of the message

api = OpenAI()
MODEL_TYPE = "gpt-4o"
punctuation = {"\n", ".", "!", "?", ";"}

stat_folder = "data_userstudy/"
if not os.path.exists(stat_folder):
    os.makedirs(stat_folder)


def count_words(text):
    # Simple word count assuming words are space-separated
    return len(text.split())


def handle_client(client_socket):
    print("New connection from {}".format(client_socket.getpeername()))
    # create a csv file to store statistics using time stamp as the file name
    stat_file_name = stat_folder + f"{time.strftime('%Y%m%d-%H%M%S')}.csv"
    log_file_name = stat_folder + f"{time.strftime('%Y%m%d-%H%M%S')}.txt"
    with open(stat_file_name, "w") as f:
        f.write(
            "Total Tokens, Input Tokens, Output Tokens, Generation Time, CommandTypes, End Time\n"
        )
    messages = []
    image_path = ""
    request_fulfill_start_time = 0
    request_fulfill_end_time = 0
    while True:
        # Receive HEAD
        head = client_socket.recv(HEAD_LEN).decode("utf-8")
        if not head:
            break
        role = int(head[0])
        msg_len = int(head[1:])
        print("Received new message head: \n", head)
        if role != 4:
            # Receive message from Unity client
            message = b""
            while len(message) < msg_len:
                message += client_socket.recv(msg_len - len(message))
            message = message.decode("utf-8")
            print("Received new message: \n", message)
        else:
            # receive bytes for image, make sure all bytes are received
            message = b""
            while len(message) < msg_len:
                message += client_socket.recv(msg_len - len(message))
            # save the image to a file
            image_path = stat_folder + f"whiteboard.png"
            with open(image_path, "wb") as f:
                f.write(message)
        if not message:
            break
        if role == 0:
            if len(image_path) == 0:
                msg = {"role": "user", "content": message, "name": "RealUser"}
            else:
                # Getting the base64 string
                with open(image_path, "rb") as image_file:
                    base64_image = base64.b64encode(image_file.read()).decode("utf-8")
                msg = {
                    "role": "user",
                    "content": [
                        {"type": "text", "text": message},
                        {
                            "type": "image_url",
                            "image_url": {
                                "url": f"data:image/jpeg;base64,{base64_image}"
                            },
                        },
                    ],
                    "name": "RealUser",
                }
            messages.append(msg)
            # Forward the message to OpenAI API
            response = api.chat.completions.create(
                model=MODEL_TYPE,
                messages=messages,
                stream=True,
            )
            print("Intialize an API request.")
            
            sentence = ""
            all_responses = ""
            list_command = []
            for chunk in response:
                if (
                    chunk.choices[0].delta.content is not None
                    or chunk.choices[0].finish_reason is not None
                ):
                    txt = chunk.choices[0].delta.content
                    if txt is not None:
                        sentence += txt
                    # do not miss the last sentence
                    if (
                        chunk.choices[0].finish_reason is not None
                    ) or any(char in punctuation for char in txt):
                        sentence = sentence.strip()
                        letters = "".join(re.findall(r"[a-zA-Z]", sentence))
                        # ignore json word or pure symbols
                        if letters == "json" or len(letters) == 0:
                            continue
                        # handle the number that includes ., e.g., 1.0
                        if sentence[-2].isdigit():
                            continue
                        # count the number of '(' in the sentence, handling (positions)
                        cnt_open_bracket = sentence.count("(") + sentence.count("{")
                        cnt_close_bracket = sentence.count(")") + sentence.count("}")
                        if cnt_open_bracket > cnt_close_bracket:
                            continue
                        # handle the type of response and send it back to Unity client
                        if "commandType" in sentence:
                            msg_type = 0  # command
                            # extract command type from the format 'commandType': 'type', add error handling
                            try:
                                command_type = (
                                    sentence.split(",")[0].split(":")[1].strip()
                                )
                                list_command.append(command_type)
                            except Exception as error:
                                print("error: ", error)
                        elif "{" in sentence and "}" in sentence:
                            if "prefab" in sentence:
                                msg_type = 1  # prefab
                            elif "action" in sentence:
                                msg_type = 3  # action
                            else:
                                msg_type = 9
                                print("Invalid message type")
                        else:
                            msg_type = 2  # text
                        list_command.append(msg_type)
                        all_responses += sentence
                        # format to HEAD_LEN
                        msg_head = str(msg_type) + str(len(sentence)).rjust(
                            HEAD_LEN - 1
                        )
                        sentence = msg_head + sentence
                        sentence += "\r\n"
                        print(sentence)
                        # Send the response back to Unity client
                        client_socket.sendall(sentence.encode("utf-8"))
                        sentence = ""
            # indicate end of response
            msg_type = 9  # end of response
            msg_head = str(msg_type) + str(0).rjust(HEAD_LEN - 1)
            msg_head += "\r\n"
            client_socket.sendall(msg_head.encode("utf-8"))
            request_fulfill_end_time = time.time()
            # record the statistics
            try:
                # get input token from messages list
                input_token = num_tokens_from_messages(messages)
                # get output token from all_responses
                output_token = num_tokens_from_response(all_responses)
                total_token = input_token + output_token
                # save the time in 3 decimal places
                fulfil_time = round(
                    request_fulfill_end_time - request_fulfill_start_time, 3
                )
                current_time = time.strftime("%Y%m%d-%H%M%S")
                str_list_command = " ".join([str(elem) for elem in list_command])
                with open(stat_file_name, "a") as f:
                    f.write(
                        f"{total_token}, {input_token}, {output_token}, {fulfil_time}, {str_list_command}, {current_time}\n"
                    )
                with open(log_file_name, "a") as f:
                    f.write(f"{messages}\n{all_responses}\n")
            except Exception as error:
                # print the error message
                print("error: ", error)
            # clear the message list
            messages = []
            image_path = ""
            print("API request finished.")
        elif role == 1:
            msg = {"role": "assistant", "content": message}
            messages.append(msg)
        elif role == 2:
            msg = {"role": "system", "content": message}
            messages.append(msg)
            request_fulfill_start_time = time.time()
        elif role == 3:
            msg = {"role": "user", "content": message, "name": "ExampleUser"}
            messages.append(msg)
        elif role == 4:
            # utilize image
            print("Received image.")
        else:
            print("Invalid role")
            break

    client_socket.close()
    print("Connection closed")


def start_server():
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.bind(("0.0.0.0", 8085))
    server.listen()
    print("Server started, listening on port 8085")

    while True:
        client, address = server.accept()
        # set tcp no delay
        client.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        thread = threading.Thread(target=handle_client, args=(client,))
        thread.start()


if __name__ == "__main__":
    start_server()
