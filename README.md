# About
This is an app I made to be able to detect anything that you want it to detect kinda like a ring, hence the name ring zero.

# Usage
The main dependencies for the ai part of this porject is ollama and gemini
Both are an option but you only need one

## Ollama
If you want to use ollama for this project first thing first you need to download ollama

Download At: https://ollama.com/

Once you downloaded ollama you need to get llava

    ollam pull llava

or 

    ollama run llava

You have to specifically use llava for this project.
## Gemini
If you want to use gemini all you need to do is either grab your real API key or you want to get one

If you don't have one you can get one for free

Get Api Key At: https://ai.google.dev/gemini-api/docs/api-key

Specs
    
    15 requests per minute
    1 million token limit
## source.json
```json
{
  "source": <source>,
  "gemini" : {boolean},
  "key" : {api_key},
  "task" : {what to detect},
  "keyword" : {what to find},
  "rate" : {time in milliseconds}
}
```

### source
Source is video feed that the app receives
Positive numbers are different camera ports
If you set source to  a negative number it will decided to receive input from server sockets
This is where the code in the Camera folder can be used
### gemini
This is a boolean that just states whether you want to use gemini or not
### key
This is the api key for your llm (only applies when using gemini)
### task
This is what you want the app to detect

Example

    Say yes when a dog is in the image
You should format the prompt to ask for a keyword
### keyword
The keyword is what is detected by the program. It's the determiner on whether you are notified or not.
Make sure you keyword matches up with your prompt

Example

    task: "Say yes when a dog is in the image"
    keyword: "yes"
Do note that the app will default to yes as a keyword if one is not provided
### rate
A time in between each llm request
The time is recorded in milliseconds
Note that the pause only exists for the gemini requests