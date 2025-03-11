import tiktoken

'''
Refer to https://cookbook.openai.com/examples/how_to_count_tokens_with_tiktoken/ for more information.
'''

def num_tokens_from_messages(messages, model="gpt-3.5-turbo-0613"):
    """Return the number of tokens used by a list of messages."""
    try:
        encoding = tiktoken.encoding_for_model(model)
    except KeyError:
        print("Warning: model not found. Using cl100k_base encoding.")
        encoding = tiktoken.get_encoding("cl100k_base")
    if model in {
        "gpt-3.5-turbo-0613",
        "gpt-3.5-turbo-16k-0613",
        "gpt-4-0314",
        "gpt-4-32k-0314",
        "gpt-4-0613",
        "gpt-4-32k-0613",
        "gpt-4o",
        "gpt-4o-2024-05-13",
        }:
        tokens_per_message = 3
        tokens_per_name = 1
    elif model == "gpt-3.5-turbo-0301":
        tokens_per_message = 4  # every message follows <|start|>{role/name}\n{content}<|end|>\n
        tokens_per_name = -1  # if there's a name, the role is omitted
    elif "gpt-3.5-turbo" in model:
        print("Warning: gpt-3.5-turbo may update over time. Returning num tokens assuming gpt-3.5-turbo-0613.")
        return num_tokens_from_messages(messages, model="gpt-3.5-turbo-0613")
    elif "gpt-4" in model:
        print("Warning: gpt-4 may update over time. Returning num tokens assuming gpt-4-0613.")
        return num_tokens_from_messages(messages, model="gpt-4-0613")
    else:
        raise NotImplementedError(
            f"""num_tokens_from_messages() is not implemented for model {model}. See https://github.com/openai/openai-python/blob/main/chatml.md for information on how messages are converted to tokens."""
        )
    num_tokens = 0
    for message in messages:
        num_tokens += tokens_per_message
        for key, value in message.items():
            num_tokens += len(encoding.encode(value))
            if key == "name":
                num_tokens += tokens_per_name
    num_tokens += 3  # every reply is primed with <|start|>assistant<|message|>
    return num_tokens

def num_tokens_from_response(response_text, model="gpt-3.5-turbo-0613"):
    """Return the number of tokens used by the response text."""
    try:
        encoding = tiktoken.encoding_for_model(model)
    except KeyError:
        print("Warning: model not found. Using cl100k_base encoding.")
        encoding = tiktoken.get_encoding("cl100k_base")
    response_tokens = len(encoding.encode(response_text))
    return response_tokens

if __name__ == "__main__":
    from openai import OpenAI
    from dotenv import load_dotenv
    import time
    
    messages = [
    {"role": "system", "content": "You are a helpful, pattern-following assistant that translates corporate jargon into plain English."},
    {"role": "system", "name":"example_user", "content": "New synergies will help drive top-line growth."},
    {"role": "system", "name": "example_assistant", "content": "Things working well together will increase revenue."},
    {"role": "system", "name":"example_user", "content": "Let's circle back when we have more bandwidth to touch base on opportunities for increased leverage."},
    {"role": "system", "name": "example_assistant", "content": "Let's talk later when we're less busy about how to do better."},
    {"role": "user", "content": "This late pivot means we don't have time to boil the ocean for the client deliverable."},
    ]

    model = "gpt-4o"

    t_tokenize_prompt_start = time.time()
    print(f"{num_tokens_from_messages(messages, model)} prompt tokens counted.")
    # Should show ~126 total_tokens
    t_tokenize_prompt_end = time.time()

    # test token usage when calling the API
    load_dotenv()
    api = OpenAI()
    
    t_request_start = time.time()
    response = api.chat.completions.create(
            model=model,
            messages=messages,
            temperature=0.0,
        )
    t_request_end = time.time()
    print(f'{response.usage.prompt_tokens} prompt tokens used.')

    # Extract the response text
    response_msg = response.choices[0].message
    # Calculate response tokens
    t_tokenize_response_start = time.time()
    response_tokens = num_tokens_from_response(response_msg.content, model)
    t_tokenize_response_end = time.time()
    print(f'{response_tokens} completion tokens calculated.')

    # Print token usage details
    print(f'{response.usage.completion_tokens} completion tokens used.')
    print(f'{response.usage.total_tokens} total tokens used.')

    print(f"Token counting took {t_tokenize_prompt_end - t_tokenize_prompt_start} seconds for the prompt and {t_tokenize_response_end - t_tokenize_response_start} seconds for the response.")
    print(f"API request took {t_request_end - t_request_start} seconds.")
