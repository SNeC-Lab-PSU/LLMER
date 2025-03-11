from openai import OpenAI
from dotenv import load_dotenv
import time
import os

current_file_path = os.path.abspath(__file__)
current_directory = os.path.dirname(current_file_path)
# load the .env file containing your API key
load_dotenv(dotenv_path=current_directory + "/.env")

print("Your API key is: ", os.getenv("OPENAI_API_KEY"))

client = OpenAI()

# %% test chat API
start_time = time.time()
completion = client.chat.completions.create(
    model="gpt-4o",
    messages=[{"role": "user", "content": "Say this is a test!"}],
    stream=True,
)

end_time = time.time()
print("Time elapsed: ", end_time - start_time)
times = []
deltas = []
chunks = []
for chunk in completion:
    chunks.append(chunk)
    if chunk.choices[0].finish_reason is not None:
        print("\n" + chunk.choices[0].finish_reason)
    if chunk.choices[0].delta.content is not None:
        print(chunk.choices[0].delta.content, end="")
        deltas.append(chunk.choices[0].delta.content)
        end_time = time.time()
        times.append(end_time - start_time)
end_time = time.time()
print("Time elapsed: ", end_time - start_time)
