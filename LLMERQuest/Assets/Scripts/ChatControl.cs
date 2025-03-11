using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using System.IO;
using Newtonsoft.Json.Linq;
using Oculus.Interaction.HandGrab;

/* This script is used to control the chat system. 
 * It will be used to send messages to ChatGPT and receive messages from it. 
 * It will also be used to handle the commands to process messages. 
 **/
public class ChatControl : MonoBehaviour
{
    EnvCreateControl envControl;
    AudioInput audioControl;
    LibraryContext contextLibrary;
    LibraryAnimation animationLibrary;
    MixedRealityManager MRManager;

    private TcpClient client;
    private NetworkStream stream;
    // stream reader
    private StreamReader reader;

    int HEAD_LENGTH = 10;
    bool keepListening = false;
    public string testMsg = "";
    public bool EvaMode = false;
    private ConcurrentQueue<PyChatResponse> MsgPool = new ConcurrentQueue<PyChatResponse>();

    string lastMsg = "";

    List<float> post_process_times = new List<float>();
    List<System.DateTime> res_req_time = new List<System.DateTime>();
    List<System.DateTime> res_end_time = new List<System.DateTime>();
    List<int> req_type = new List<int>();
    private List<int> lastSavedIndices = new List<int>();
    float lastSavedTime = 0.0f;

    public string path = "./";

    string jsonSchema = @"{
    ""type"": ""object"",
    ""properties"": {
        ""commandType"": {
            ""type"": ""string"",
            ""enum"": [
                ""environment"",
                ""animation"",
                ""MRinteraction""
            ],
            ""descriptions"": {
                ""environment"": ""Create a new environment or add objects to the current scene. Only used for 'new' object."",
                ""animation"": ""Generate animations, edit or remove objects, can be either related to you (robot) or the user or other objects in the scene."",
                ""MRinteraction"": ""Only use this commandType when the request is associted to drawing or writing.""
            },
            ""description"": ""Defines the type of command to be executed.""
        },
        ""request"": {
            ""type"": ""string"",
            ""description"": ""The user's request or question.""
        },
        ""MRtype"": {
            ""type"": ""string"",
            ""enum"": [
                ""recognition"",
                ""conversion"",
                ""clearance""
            ],
            ""description"": ""Only required when commandType is 'MRinteraction'. Differentiate the user request if recognizing drawing, or converting the drawing to objects, or simply clear previous drawing. ""
        },  
        ""clearEnv"": {
            ""type"": ""boolean"",
            ""description"": ""This flag should be set to true if requesting a clearance of old scenes, for example, when creating a new scene.""
        },
        ""robot"": {
            ""type"": ""boolean"",
            ""description"": ""This flag should be set to true if requesting contextual data associated to the robot, i.e., the agent itself. Required when commandType is 'animation'.""
        },   
        ""scene"": {
            ""type"": ""boolean"",
            ""description"": ""This flag should be set to true if requesting contextual data associated to the scene, like the properties of existing objects.""
        }, 
        ""user"": {
            ""type"": ""boolean"",
            ""description"": ""This flag should be set to true if requesting the contextual information associtated to the user.""
        },
        ""resource"": {
            ""type"": ""boolean"",
            ""description"": ""This flag must be set to true if commandType is environment""
        },
        ""animationData"": {
            ""type"": ""boolean"",
            ""description"": ""This flag must be set to true if commandType is animation.""
        },
        ""position"": {
            ""type"": ""boolean"",
            ""description"": ""This flag should be set to true if requesting the position of the objects.""
        },
        ""orientation"": {
            ""type"": ""boolean"",
            ""description"": ""This flag should be set to true if requesting the orientation of the objects.""
        },
        ""scale"": {
            ""type"": ""boolean"",
            ""description"": ""This flag should be set to true if requesting the scale of the objects.""
        },
        ""size"": {
            ""type"": ""boolean"",
            ""description"": ""This flag should be set to true if requesting the size of the objects.""
        }
    },
    ""required"": [""commandType"", ""request""]
}";


    // Start is called before the first frame update
    void Start()
    {
        // acquire control scripts
        envControl = this.GetComponent<EnvCreateControl>();
        audioControl = this.GetComponent<AudioInput>();
        contextLibrary = this.GetComponent<LibraryContext>();
        animationLibrary = this.GetComponent<LibraryAnimation>();
        MRManager = this.GetComponent<MixedRealityManager>();

        // initialize the chat
        InitChat();

        for (int i = 0; i < 5; i++)
        {
            lastSavedIndices.Add(0);
        }
        lastSavedTime = Time.realtimeSinceStartup;
    }

    private async void InitChat()
    {
        // initialize the TCP connection
        client = new TcpClient(Utils.ServerIP, Utils.ServerPort);
        stream = client.GetStream();
        reader = new StreamReader(stream);

        // create background thread to listen to the chat
        keepListening = true;

        Task.Run(() => ReceiveMessages());

        Debug.Log("Initialization done.");
    }

    // Update is called once per frame
    void Update()
    {
        while (MsgPool.TryDequeue(out var res))
        {
            Debug.Log(res.msg);
            ParseCommand(res.MsgType, res.msg);
        }
        // save time data every 5 seconds
        if (EvaMode && Time.realtimeSinceStartup - lastSavedTime > 5)
        {
            lastSavedTime = Time.realtimeSinceStartup;
            SaveData();
        }
    }

    public async Task<string> GetResponse(string msg)
    {
        if (EvaMode)
        {
            res_req_time.Add(System.DateTime.Now);
            req_type.Add(2); // 2 for plain text
        }
        lastMsg = msg;
        Utils.AddUserMessage(msg);
        string TotalMsg = "";
        // add default prompt
        string SysMsg = "You are a virtual agent in a Mixed Reality application. " +
            "You are responsible to understand the user's request and provide responses in JSON format or plain texts." +
            "The following is the JSON schema for your response: \n" +
            jsonSchema + "\n" +
            "Provide proper JSON data if the user's request falls into the description of provided command types. If the request is out of the scope of the description, provide plain texts.\n" +
            "Try to decompose the complex request into a series of simple commands. Note a request is complex only when it needs multiple command types, otherwise, just keep the user's message as the request.\n" +
            "If the user asks about something else not match the description of JSON schema, just return plain texts.\n" +
            "Avoid using special characters in your responses except specified by the JSON data. \n";
        if (Utils.GetUserMessageCount() > 0)
            SysMsg += "The user may not clearly specify the object in the request, use the following previous " + Utils.GetUserMessageCount() + " messages from the user to infer the target if ambiguous pronoun is used: \n" + Utils.GetUserMessages() + "\n";

        SysMsg += "The following are some examples of conversations to cope with user request, replace those placeholders indicated by <> with contextual data.\n";
        // give a few examples as user and assistant
        // user example message
        SysMsg += "User: Can you create a <environment> for me?";
        // assistant message
        SysMsg += "Assistant: I will first analyze the available resources and let you know my design.\n" +
            "{'commandType': 'environment', 'request': 'create a <environment>', 'resource': true, 'size': true, 'clearEnv': true}\n";
        // user example message
        SysMsg += "User: Remove everything.";
        // assistant message
        SysMsg += "Assistant: I will remove all the created objects in the scene.\n" +
            "{'commandType': 'environment', 'clearEnv': true}\n";
        // user example message
        SysMsg += "User: Can you add three <object> to the scene?";
        // assistant message
        SysMsg += "Assistant: I will first analyze the available resources and try to add those objects.\n" +
            "{'commandType': 'environment', 'request': 'add three <object> to the scene', 'resource': true, 'scene': true, 'position': true, 'size': true}\n";


        // user example message
        SysMsg += "User: Change the color of the <object> to cyan.";
        // assistant message
        SysMsg += "Assistant: Let me check the object in the environment.\n" +
            "{'commandType': 'animation', 'request': 'change the color of the <object> to cyan', 'scene': true, 'robot': true}\n";

        SysMsg += "User: Keep your eyes on the <object>.";
        SysMsg += "Assistant: Let me analyze the request.\n" +
            "{'commandType': 'animation', 'request': 'Keep the robot eyes on the <object>', 'scene': true, 'position': true}\n";

        // user example message
        SysMsg += "User: Can you hand the <object> to me?";
        // assistant message
        SysMsg += "Assistant: Let me check the object in the environment.\n" +
            "{'commandType': 'animation', 'request': 'bring the <object> to the user', 'robot': true, 'scene': true, 'user': true, 'position': true}\n";
        // user example message
        SysMsg += "User: Can you create a rotating <object>?";
        // assistant message
        SysMsg += "Assistant: Let me analyze the request.\n" +
            "{'commandType': 'environment', 'request': 'add a <object> to the scene', 'resource': true, 'scene': true, 'position': true, 'size': true}\n" +
            "{'commandType': 'animation', 'request': 'let the <object> be rotating', 'scene': true, 'robot': true}\n";
        // user example message
        SysMsg += "User: Stop the running <object>.";
        // assistant message
        SysMsg += "Assistant: Let me analyze the request.\n" +
            "{'commandType': 'animation', 'request': 'stop the running <object>', 'scene': true, 'robot': true, 'animationData': true}\n";
        // user example message
        SysMsg += "User: Can you create a <object> three times larger than usual?";
        // assistant message
        SysMsg += "Assistant: Let me analyze the request.\n" +
            "{'commandType': 'environment', 'request': 'add a <object> to the scene', 'resource': true, 'scene': true, 'position': true, 'size': true}\n" +
            "{'commandType': 'animation', 'request': 'let the <object> be three times larger', 'scene': true, 'robot': true, 'scale': true}\n";
        // user example message
        SysMsg += "User: Catch me the <object>.";
        // assistant message
        SysMsg += "Assistant: Let me analyze the request.\n" +
            "{'commandType': 'animation', 'request': 'let the robot catch the <object> and bring to the user position', 'scene': true, 'robot': true, 'user': true, 'position': true}\n";
        // user example message
        SysMsg += "User: Do you understand what I am writing/drawing?";
        // assistant message
        SysMsg += "Assistant: Let me analyze your writing/drawing.\n" +
            "{'commandType': 'MRinteraction', 'request': 'analyze the user writing on image', 'MRtype': 'recognition'} \n";
        // user example message
        SysMsg += "User: Can you convert my drawing to objects?";
        // assistant message
        SysMsg += "Assistant: Let me analyze your drawing.\n" +
            "{'commandType': 'MRinteraction', 'request': 'convert the user drawing to objects', 'MRtype': 'conversion'} \n";
        // user example message
        SysMsg += "User: Who are you?";
        // assistant message
        SysMsg += "Assistant: I am a virtual agent in VR. \nI can interact with you and the environment.";

        // give instruction as System
        SendMessage(2, SysMsg);
        // add user message to the chat
        SendMessage(0, msg); // user
        Debug.Log("waiting for response...");
        return TotalMsg;
    }

    void ParseCommand(int type, string msg)
    {
        if (type == 0)
        {
            // initial response
            Regex regex = new Regex(@"\{.*\}");
            Match match = regex.Match(msg);
            if (!match.Success)
            {
                Debug.LogError("No valid JSON found.");
                return;
            }
            string jsonInput = match.Value;
            JObject jdata = JObject.Parse(jsonInput);
            string commandType = jdata["commandType"]?.Value<string>();
            // Handle commandType
            switch (commandType.ToLower())
            {
                case "environment":
                    bool clearEnv = jdata["clearEnv"]?.Value<bool>() ?? false;
                    bool append = !clearEnv;
                    envControl.IndicateConstruct();
                    // add property 'resource' to indicate the request is for creating new objects
                    if (jdata["resource"] == null)
                        jdata.Add("resource", true);
                    // force to get the size information
                    if (jdata["size"] == null)
                        jdata.Add("size", true);
                    StartCoroutine(SendEnvRequest(jdata));
                    Debug.Log("Environment creation command received.");
                    break;
                case "animation":
                    // add property 'animationData' to indicate the request is for generating animations
                    if (jdata["animationData"] == null)
                        jdata.Add("animationData", true);
                    if (jdata["scene"] == null)
                        jdata.Add("scene", true);
                    StartCoroutine(SendInteractRequest(jdata));
                    Debug.Log("Interaction command received.");
                    break;
                case "mrinteraction":
                    SendMRRequest(jdata);
                    break;
                case "-1":
                    // end of action
                    // indicate the end of environment creation if it is in the process
                    envControl.IndicateIdle();
                    Debug.Log("End of agent action.");
                    break;
                default:
                    Debug.LogError("Invalid command type.");
                    break;
            }
        }
        else if (type == 1)
        {
            // environment creation
            float s_time = Time.realtimeSinceStartup;
            Regex regex = new Regex(@"\{.*\}");
            Match match = regex.Match(msg);
            if (!match.Success)
            {
                Debug.LogError("No valid JSON found.");
            }
            else
            {
                string jsonInput = match.Value;
                JObject objCreate = JObject.Parse(jsonInput);
                CreateObjEnv(objCreate);
            }
            float e_time = Time.realtimeSinceStartup;
            float post_process_time = e_time - s_time;
            if (EvaMode)
                post_process_times.Add(post_process_time);
        }
        else if (type == 2)
        {
            // plain text, generate audio
            audioControl.EnqueAudioMsg(msg);
        }
        else if (type == 3)
        {
            // movement control 
            Regex regex = new Regex(@"\{.*\}");
            Match match = regex.Match(msg);
            if (!match.Success)
            {
                Debug.LogError("No valid JSON found.");
            }
            else
            {
                string jsonInput = match.Value;
                JObject animation = JObject.Parse(jsonInput);
                animationLibrary.AddToAnimationPool(animation);
            }
        }
        else if (type == 9)
        {
            Debug.Log("Response received.");
        }
        else
        {
            Debug.LogError("Invalid request type.");
        }
    }


    #region animated interaction

    IEnumerator SendInteractRequest(JObject jdata)
    {
        string userMsg = jdata["request"]?.Value<string>();
        if (userMsg == null)
        {
            Debug.LogError("No valid animation request found.");
            yield break;
        }
        // block the acquization of scene data if the environment is under construction
        // block at max 5 seconds
        float duration = 0;
        while (!envControl.IsIdle() && duration < 10)
        {
            duration += Time.deltaTime;
            yield return null;
        }
        if (EvaMode)
        {
            res_req_time.Add(System.DateTime.Now);
            req_type.Add(3); // 3 for animated interaction
        }
        string SysMsg = "You are a virtual agent in a Mixed Reality application. " +
            "You are responsible for following the user's instruction to conduct various tasks." +
            "Be extremely careful for the following contextual information: \n{\n" +
            contextLibrary.GetContextData(jdata) + "\n}\n" +
            "If an object partially meets the description provided by the user, issue the necessary command to fulfill the user's request as closely as possible.\n" +
            "If there are multiple objects that satisfy the description to some extent, select the one that matches most given the contexts.\n" +
            "If no object closely or partially matches the user's request, inform the user of this with a brief sentence. No more detailed explanation." +
            "For real-world objects and the user's hands, use the uuid as the object name when serving as 'target'. \n" +
            "Your response need to include specific JSON data following the JSON schema: \n" +
            animationLibrary.GetSchema() + "\n" +
            "Note that Axis (1,0,0) is right direction, (0,1,0) is up direction, (0,0,1) is forward direction.\n" +
            "Always start with a 'looktowards' action to face the object for each request. \n" +
            "Remember to use 'looktowards' before each 'movetowards' action when moving the robot itself to mimic a natual animation. \n" +
            "The movement can be classified into two cases: directly move to the target position with a random direction or rotate towards the target position then move in forward direction (always positive values '0 0 <number>'). Select the one which you think is more natural.\n" +
            "If you find there is an existing animation similar to the user's request, stop the existing animation and start the new one.\n" +
            "Answer in the fixed format.\n" +
            "'''\n{'action': 'actionName', 'object': 'object name', 'id': 'animation name', ...}\n{...}'''\n" +
            "You may have one or more of actions.\n" +
            "Make sure following those format when returning the actions and no comma between numbers.\n" +
            "Try to avoid the obstacles from the object list and make sure the position and orientation are correct. \n" +
            "First think about the categories and orders of animations required, then generate the data. Give a very brief description on what you will do before JSON data.";

        SysMsg += "The following are some examples of conversations to cope with user request, replace those placeholders indicated by <> with contextual data.\n";

        // give a few examples as user and assistant, which may be related to the user's request

        // those properties can be used to provide proper examples
        bool reqScene = jdata["scene"]?.Value<bool>() ?? false;

        if (reqScene)
        {
            if (jdata["size"] != null)
            {
                SysMsg += "User: Make the <objectName> two times larger.";
                SysMsg += "Assistant: Sure. I will make it bigger.\n" +
                    "'''\n" +
                    "{'action': 'looktowards', 'object': 'Robot', 'target': '<objectName>', 'id': 'lookTowards<objectName>'}\n" +
                    "{'action': 'scale', 'object': '<objectName>', 'scale': '<number> <number> <number>', 'id': 'scaleTableTwoTimesLarger', 'time': <number>}\n" +
                    "'''\n";
            }


            SysMsg += "User: Keep your eyes on the '<objectName>'.";
            SysMsg += "Assistant: Sure. I will create the animation 'gazing<objectName>'.\n" +
                "'''\n" +
                "{'action': 'looktowards', 'object': 'Robot', 'target': '<objectName>', 'id': 'lookTowards<objectName>'}\n" +
                "{'action': 'gazing', 'object': 'Robot', 'target': '<objectName>', 'id': 'gazing<objectName>'}\n" +
                "'''\n";

            SysMsg += "User: Let the '<objectName>' rotate 180 degrees.";
            SysMsg += "Assistant: Sure. I will let it rotate towards the target orientation.\n" +
                "'''\n" +
                "{'action': 'looktowards', 'object': 'Robot', 'target': '<objectName>', 'id': 'lookTowards<objectName>'}\n" +
                "{'action': 'selfrotate', 'object': '<objectName>', 'axis': '<number> <number> <number>', 'speedRot': <number>, 'time': <number>, 'id': 'rotating<objectName>180'}\n" +
                "'''\n";

            SysMsg += "User: Let the '<objectName>' be rotating around my right index finger.";
            SysMsg += "Assistant: Sure. I will let it be rotating.\n" +
                "'''\n" +
                "{'action': 'looktowards', 'object': 'Robot', 'target': '<fingerUUid>', 'id': 'lookTowardsRightIndexFinger'}\n" +
                "{'action': 'orbit', 'object': '<objectName>', 'target': '<fingerUUid>', 'speedRot': <number>, 'id': 'orbit<objectName>AroundRightIndexFinger'}\n" +
                "'''";

            SysMsg += "User: Move the '<objectName>' three meters away in its right direction.";
            SysMsg += "Assistant: Sure.\n" +
                "'''\n" +
                "{'action': 'looktowards', 'object': 'Robot', 'target': '<objectName>', 'id': 'lookTowards<objectName>'}\n" +
                "{'action': 'movetowards', 'object': '<objectName>', 'localposition': '3 0 0', 'speedMov': <number>, 'id': 'move<objectName>Away'}\n" +
                "'''\n";

            SysMsg += "User: Move yourself three meters away on the right.";
            SysMsg += "Assistant: Sure.\n" +
                "'''\n" +
                "{'action': 'looktowards', 'object': 'Robot', 'localposition': '3 0 0', 'id': 'lookTowards<objectName>'}\n" +
                "{'action': 'movetowards', 'object': '<objectName>', 'localposition': '0 0 3', 'speedMov': <number>, 'id': 'move<objectName>Away'}\n" +
                "'''\n" +
                "Note: In this example, after the rotation, the original right direction become the forward direction of the robot. So you should use '0 0 3' as local position instead of '3 0 0'.\n";

            SysMsg += "User: Move the '<objectName>' three meters away in my right direction.";
            SysMsg += "Assistant: Sure.\n" +
                "'''\n" +
                "{'action': 'looktowards', 'object': 'Robot', 'target': '<objectName>', 'id': 'lookTowards<objectName>'}\n" +
                "{'action': 'movetowards', 'object': '<objectName>', 'localdirection': '1 0 0', 'distance': 3, 'target': 'CenterEyeAnchor', 'speedMov': <number>, 'id': 'move<objectName>Away'}\n" +
                "'''\n";

            SysMsg += "User: Where is the '<objectName>'?";
            SysMsg += "Assistant: It is on my eye sight.\n" +
                "'''\n" +
                "{'action': 'looktowards', 'object': 'Robot', 'target': '<objectName>', 'id': 'lookTowards<objectName>'}\n" +
                "'''\n";
        }

        if (userMsg.ToLower().Contains("bring") || userMsg.ToLower().Contains("catch"))
        {
            SysMsg += "User: Bring me the '<objectName>'.";
            SysMsg += "Assistant: No problem.\n" +
                "'''\n" +
                "{'action': 'looktowards', 'object': 'Robot', 'target': '<objectName>', 'id': 'rotateRobotToPos1'}\n" +
                "{'action': 'movetowards', 'object': 'Robot', 'target': '<objectName>', 'safebound': '<number>', 'id': 'moveRobotToPos1'}\n" +
                "{'action': 'catch', 'object': 'HandR', 'target': '<objectName>', 'safebound': '-1', 'id': 'useHandRToCatch<objectName>'}\n" +
                "{'action': 'movetowards', 'object': 'HandR', 'localposition': '<number> <number> <number>', 'id': 'moveHandRBack'}\n" +
                "{'action': 'looktowards', 'object': 'Robot', 'position': '<number> <number> <number>', 'id': 'rotateRobotToPlayer'}\n" +
                "{'action': 'movetowards', 'object': 'Robot', 'position': '<number> <number> <number>', 'id': 'moveRobotToPlayer'}\n" +
                "{'action': 'detach', 'object': '<objectName>', 'id': 'detach<objectName>FromHand'}\n" +
                "'''\n";
        }

        SysMsg += "User: Stop focusing on the '<objectName>'.";
        SysMsg += "Assistant: Sure. I will stop the animation '<AnimationID>'.\n" +
            "'''\n" +
            "{'action': 'looktowards', 'object': 'Robot', 'target': '<objectName>', 'id': 'lookTowards<objectName>'}\n" +
            "{'action': 'stop', 'object': '<objectName>', 'id': '<AnimationID>'}\n" +
            "'''\n";

        SendMessage(2, SysMsg);

        // add user message to the chat
        SendMessage(0, userMsg); // user
        Debug.Log("waiting for response...");
    }

    #endregion


    #region environment creation

    IEnumerator SendEnvRequest(JObject jdata)
    {
        bool clearEnv = jdata["clearEnv"]?.Value<bool>() ?? false;
        // destroy previous objects
        if (clearEnv)
            StartCoroutine(envControl.DestroyObjs());

        string userMsg = jdata["request"]?.Value<string>();
        if (userMsg == null)
        {
            Debug.LogWarning("No valid environment creation request found.");
            yield break;
        }

        if (EvaMode)
        {
            res_req_time.Add(System.DateTime.Now);
            req_type.Add(1); // 1 for environment creation
        }

        // give instruction as System
        string SysMsg = "You are a virtual agent in a Mixed Reality application. " +
            "You are responsible for following the user's instruction to conduct various tasks.\n" +
            contextLibrary.GetContextData(jdata) +
            "When asked to create or add something to the environment, determine the required prefabs and their quantities. " +
            "If proper prefabs are unavailable, use Unity primitives to create the objects." +
            "Simply state you will use existing resources or Unity primitives instead of details in your response.\n" +
            "Provide JSON data following this schema: \n" +
            envControl.GetSchema() + "\n" +
            "Adhere to the following rules:\n" +
            "1. Utilize the object whose property matches the context most appropriately when multiple objects satisfy the request.\n" +
            "2. Estimate the space objects will occupy based on provided position and size. For example, when placing an object on a table, limit its position within the table's boundaries on the x and z axes.\n" +
            "3. Generate correct 'prefabType' with prefabs resources or Unity primitives, not mixed with existing object names.\n" +
            "4. Adjust the scale of objects to fit the scene based on their size; a scale of 1 refers to 1 meter for Unity primitives.\n" +
            "5. Specify a 'parent' object if one object needs to be placed or based on another. Use 'localposition' to specify the relative position to the parent object.\n" +
            "6. For real-world objects, use the UUID as the object name when serving as 'parent'. A localposition of (0,0,0) refers to the center of the object's up surface.\n" +
            "7. When placing an object on another, use 'localposition' values close to (0,0,0) and adjust x and z values to avoid collisions. Slightly increase the y value to simulate a fall-down effect. y value in local position should always be positive when placing objects on another.\n" +
            "8. Ensure the floor and ceiling are in layer 0, objects on the floor are in layer 1, and other objects are in layer 2. List objects on lower layers first.\n" +
            "9. Create an empty object as the base and add other objects as its children for complex objects.\n" +
            "10. For orientations, Axis (1,0,0) is right, (0,1,0) is up, and (0,0,1) is forward. Rotation (0 0 0) means facing the negative z-axis, and rotation (0 90 0) means facing the negative x-axis.\n" +
            "11. Keep the layout tight and limit the number of objects to avoid complexity.\n" +
            "12. Add {commandType: -1} to the end of your response to indicate the end of environment creation. No further explanations are needed.\n" +
            "The following are examples of conversations to handle user requests, replacing placeholders indicated by <> with contextual data.\n";

        // give a few examples as user and assistant

        SysMsg += "User: Can you create a classroom for me?";
        SysMsg += "Assistant: I will create a classroom using local resources.\n" +
            "'''\n" +
            "{'prefabType': 'Room', 'objectName': 'ClassRoom', 'layer': 0, 'position': '0 0 0', 'rotation': '0 0 0'}\n" +
            "{'prefabType': 'StudyDesk', 'objectName': 'StudyDesk1', 'layer': 1, 'localposition': '<number> <number> <number>', 'rotation': '<number> <number> <number>', 'parent': 'Room1'}\n" +
            "{'prefabType': 'OfficeChair', 'objectName': 'OfficeChair1', 'layer': 1, 'localposition': '<number> <number> <number>', 'rotation': '<number> <number> <number>', 'parent': 'Room1'}\n" +
            "{'prefabType': 'Pen black', 'objectName': 'PenBlack1', 'layer': 2, 'localposition': '<number> <number> <number>', 'rotation': '<number> <number> <number>', 'parent': 'StudyDesk1'}\n" +
            "{commandType: -1}'''";

        SysMsg += "User: Add some office supplies to the table.";
        SysMsg += "Assistant: Sure.\n" +
            "'''\n" +
            "{'prefabType': 'Pen black', 'objectName': 'PenBlack1', 'layer': 2, 'localposition': '0.1 0.1 -0.1', 'rotation': '0 0 0', 'parent': '<objectname>'}\n" +
            "{'prefabType': 'Calculator', 'objectName': 'Calculator1', 'layer': 2, 'localposition': '-0.1 0.1 0.1', 'rotation': '0 0 0', 'parent': '<objectname>'}\n" +
            "{commandType: -1}'''";

        SysMsg += "User: Add a cube to the scene.";
        SysMsg += "Assistant: No problem.\n" +
            "'''\n" +
            "{'prefabType': 'cube', 'objectName': 'Cube1', 'layer': 1, 'position': '<number> <number> <number>', 'rotation': '<number> <number> <number>', 'scale': '<number> <number> <number>'}\n" +
            "{commandType: -1}'''";

        SysMsg += "User: Add a small car to the scene using Unity primitive.";
        SysMsg += "Assistant: I will add a car to the scene using Unity primitives.\n" +
            "'''\n" +
            "{'prefabType': 'empty', 'objectName': 'Car', 'layer': 2, 'position': '<number> <number> <number>', 'rotation': '<number> <number> <number>'}\n" +
            "{'prefabType': 'cube', 'objectName': 'CarBody', 'layer': 2, 'localposition': '<number> <number> <number>', 'rotation': '<number> <number> <number>', 'scale': '<number> <number> <number>', 'parent': 'Car'}\n" +
            "{'prefabType': 'cylinder', 'objectName': 'CarFrontLeftWheel', 'layer': 2, 'localposition': '<number> <number> <number>', 'rotation': '<number> <number> <number>', 'scale': '<number> <number> <number>', 'color': '<number> <number> <number>', 'parent': 'Car'}\n" +
            "{'prefabType': 'cylinder', 'objectName': 'CarFrontRightWheel', 'layer': 2, 'localposition': '<number> <number> <number>', 'rotation': '<number> <number> <number>', 'scale': '<number> <number> <number>', 'color': '<number> <number> <number>', 'parent': 'Car'}\n" +
            "{'prefabType': 'cylinder', 'objectName': 'CarBackLeftWheel', 'layer': 2, 'localposition': '<number> <number> <number>', 'rotation': '<number> <number> <number>', 'scale': '<number> <number> <number>', 'color': '<number> <number> <number>', 'parent': 'Car'}\n" +
            "{'prefabType': 'cylinder', 'objectName': 'CarBackRightWheel', 'layer': 2, 'localposition': '<number> <number> <number>', 'rotation': '<number> <number> <number>', 'scale': '<number> <number> <number>', 'color': '<number> <number> <number>', 'parent': 'Car'}\n" +
            "{commandType: -1}'''";

        SendMessage(2, SysMsg);

        // add user message to the chat
        SendMessage(0, userMsg); // user
        Debug.Log("waiting for environment creation response...");
    }

    void CreateObjEnv(JObject jdata)
    {
        envControl.CreateObj(jdata);
    }

    #endregion

    #region Mixed Reality Interaction
    private async void SendMRRequest(JObject jdata)
    {
        string userMsg = jdata["request"]?.Value<string>();
        string type = jdata["MRtype"]?.ToString();
        if (type == null)
        {
            Debug.LogWarning("No valid MR interaction request found.");
            return;
        }
        if (type.Equals("clearance"))
        {
            ClearWhiteboardImg();
            return;
        }

        bool reqConversion = type.Equals("conversion");

        string imagePath = Path.Combine(UnityEngine.Application.persistentDataPath, "whiteboard.png");
        if (!SaveWhiteboardImg(imagePath))
        {
            Debug.LogWarning("Failed to save whiteboard image.");
            return;
        }
        // give instruction as System
        string SysMsg = "You are a virtual agent in a Mixed Reality application. \n";
        if (!reqConversion)
        {
            SysMsg += "You are responsible for understanding what the user is drawing or writing given this picture.";
            SendMessage(2, SysMsg);
        }
        else
        {
            SysMsg += "You are responsible for converting the user's drawing or writing into a series of objects using Unity primitives.\n" +
                "The following is the JSON schema you need to follow: \n" +
                MRManager.GetSchema() + "\n" +
                "Remember that scale 1 means the object is 1 meter in Unity. Limit the size of created objects to be visible by the user.\n" +
                "Create objects near your position: " + this.transform.position.ToString() + "\n" +
                "Briefly mention you will create objects using Unity primitives, then provide JSON data in correct format. No more explanation.";
            SysMsg += "The following are some examples of conversations to cope with user request, replace those placeholders indicated by <> with contextual data.\n";
            // give a few examples as user and assistant
            SysMsg += "User: Can you convert my drawing to objects?";
            SysMsg += "Assistant: I will convert your drawing to objects using Unity primitives.\n" +
                "'''\n" +
                "{'prefabType': '<prefabType>', 'objectName': '<objectName>', 'position': '<number> <number> <number>', 'rotation': '<number> <number> <number>', 'scale': '<number> <number> <number>'}\n" +
                "'''";
            SendMessage(2, SysMsg);
        }
        Debug.Log("waiting for Mixed Reality interaction response...");

        // indicate the usage of picture
        SendMessage(4, imagePath);

        // add user message to the chat
        SendMessage(0, userMsg); // user
    }

    bool SaveWhiteboardImg(string imagePath)
    {
        // get whiteboard in the scene
        GameObject whiteboard = MRManager.GetWhiteboard(contextLibrary.GetUserPosition());
        if (whiteboard == null) return false;
        Texture2D tex = whiteboard.GetComponent<Whiteboard>().texture;
        if (tex == null) return false;

        // save texture to file
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(imagePath, bytes);

        return true;
    }

    bool ClearWhiteboardImg()
    {
        // get whiteboard in the scene
        GameObject whiteboard = MRManager.GetWhiteboard(contextLibrary.GetUserPosition());
        if (whiteboard == null)
        {
            Debug.Log("No whiteboard found.");
            return false;
        }
        Vector2 textureSize = whiteboard.GetComponent<Whiteboard>().textureSize;
        Destroy(whiteboard.GetComponent<Whiteboard>().texture);
        Texture2D texture = new Texture2D((int)textureSize.x, (int)textureSize.y);
        whiteboard.GetComponent<Whiteboard>().texture = texture;
        whiteboard.GetComponent<Renderer>().material.mainTexture = texture;
        return true;
    }
    #endregion

    #region TCP 
    private void SendMessage(int role, string msg)
    {
        if (client == null || !client.Connected || stream == null)
        {
            return;
        }
        try
        {
            // role: 0 for user (deletable), 1 for assistant, 2 for system, 3 for user (init), 4 for image
            if (role == 4)
            {
                // send image
                byte[] imageData = File.ReadAllBytes(msg);
                string HEAD = role + imageData.Length.ToString().PadLeft(HEAD_LENGTH - 1);
                byte[] headData = Encoding.UTF8.GetBytes(HEAD);
                stream.Write(headData, 0, headData.Length);
                stream.Write(imageData, 0, imageData.Length);
                stream.Flush();
                return;
            }
            else
            {
                string HEAD = role + msg.Length.ToString().PadLeft(HEAD_LENGTH - 1);
                byte[] data = Encoding.UTF8.GetBytes(HEAD + msg);
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
        }
        catch (Exception ex)
        {
            Debug.Log("Error in SendMessage: " + ex.Message);
        }
    }

    private async void ReceiveMessages()
    {
        try
        {
            string TotalMsg = "";
            while (keepListening)
            {
                // do nothing
                PyChatResponse res = await ReceiveMessage();
                if (res == null)
                {
                    break;
                }
                TotalMsg += res.msg;
                if ((res.msg != null && res.msg.Length > 0) || res.MsgType == 9)
                    MsgPool.Enqueue(res);
            }
            keepListening = false;
            Debug.Log("Received thread closed.");
        }
        catch (Exception e)
        {
            Debug.LogError("Error in ReceiveMessages: " + e.Message);
        }
    }

    private async Task<PyChatResponse> ReceiveMessage()
    {
        if (client == null || !client.Connected || stream == null)
        {
            return null;
        }
        try
        {
            string msg = reader.ReadLine();
            // first few characters are the header
            if (msg == null)
            {
                return null;
            }

            string headStr = msg.Substring(0, HEAD_LENGTH);

            int MsgType = int.Parse(headStr.Substring(0, 1));
            if (MsgType == 9)
            {
                // end of chat response
                if (EvaMode)
                    res_end_time.Add(System.DateTime.Now);
                Debug.Log("receive end of response indicator");
                return new PyChatResponse(MsgType, "");
            }
            int msgLength = int.Parse(headStr.Substring(1));

            string info = msg.Substring(HEAD_LENGTH);
            return new PyChatResponse(MsgType, info);
        }
        catch (Exception ex)
        {
            Debug.Log("Error in ReceiveMessage: " + ex.Message);
            return null;
        }
    }

    class PyChatResponse
    {
        public string msg;
        public int MsgType;
        public PyChatResponse(int msgType, string msg)
        {
            this.MsgType = msgType;
            this.msg = msg;
        }
    }
    #endregion

    private void OnDestroy()
    {
        // close the TCP connection
        if (client != null)
        {
            client.Close();
            Debug.Log("TCP connection closed.");
        }
        if (reader != null)
        {
            reader.Close();
        }
        if (stream != null)
        {
            stream.Close();
        }
        keepListening = false;

        if (EvaMode)
            // save the data
            SaveData();
    }

    private void SaveData()
    {
        // save the data
        string filename = "Time_data_chat.csv";
        string filepath = path + filename;
        // add first line if the file does not exist
        if (!File.Exists(filepath))
        {
            using (System.IO.StreamWriter file =
                                      new System.IO.StreamWriter(filepath))
            {
                file.WriteLine("request type, response time");
            }
        }
        if (lastSavedIndices[0] < req_type.Count)
        {
            Debug.Log("response time start from: " + lastSavedIndices[0] + " total: " + res_end_time.Count);
        }
        using (System.IO.StreamWriter file =
                       File.AppendText(filepath))
        {
            for (int i = lastSavedIndices[0]; i < req_type.Count; i++)
            {
                int type = req_type[i];
                if (i >= res_end_time.Count)
                    continue;
                lastSavedIndices[0]++;
                TimeSpan ts = res_end_time[i] - res_req_time[i];
                file.WriteLine(type + "," + ts.TotalSeconds);
            }
        }
        filename = "Time_data_process.csv";
        filepath = path + filename;
        if (!File.Exists(filepath))
        {
            using (System.IO.StreamWriter file =
                                                     new System.IO.StreamWriter(filepath))
            {
                file.WriteLine("post-process time");
            }
        }
        if (lastSavedIndices[1] < post_process_times.Count)
        {
            Debug.Log("process time start from: " + lastSavedIndices[1] + " total: " + post_process_times.Count);
        }
        using (System.IO.StreamWriter file =
                       File.AppendText(filepath))
        {
            for (int i = lastSavedIndices[1]; i < post_process_times.Count; i++)
            {
                lastSavedIndices[1]++;
                file.WriteLine(post_process_times[i]);
            }
        }
        List<float> TimeEndRecord = audioControl.GetTimeEndRecord();
        List<float> TimeSendWhisper = audioControl.GetTimeSendWhisper();
        List<float> TimeRecvWhisper = audioControl.GetTimeRecvWhisper();
        filename = "Time_data_whisper.csv";
        filepath = path + filename;
        if (!File.Exists(filepath))
        {
            using (System.IO.StreamWriter file =
                                                     new System.IO.StreamWriter(filepath))
            {
                file.WriteLine("pre-process time, audio to text time");
            }
        }
        if (lastSavedIndices[2] < TimeRecvWhisper.Count)
        {
            Debug.Log("whisper time start from: " + lastSavedIndices[2] + " total: " + TimeRecvWhisper.Count);
        }
        using (System.IO.StreamWriter file =
                       File.AppendText(filepath))
        {
            for (int i = lastSavedIndices[2]; i < TimeRecvWhisper.Count; i++)
            {
                lastSavedIndices[2]++;
                if (i >= TimeEndRecord.Count)
                    file.WriteLine(0 + "," + (TimeRecvWhisper[i] - TimeSendWhisper[i]));
                else
                    file.WriteLine((TimeSendWhisper[i] - TimeEndRecord[i]) + "," + (TimeRecvWhisper[i] - TimeSendWhisper[i]));
            }
        }
        List<float> TimeSendTTS = audioControl.GetTimeSendTTS();
        List<float> TimeRecvTTS = audioControl.GetTimeRecvTTS();
        List<int> CountTTS = audioControl.GetCountTTS();
        filename = "Time_data_tts.csv";
        filepath = path + filename;
        if (!File.Exists(filepath))
        {
            using (System.IO.StreamWriter file =
                                                     new System.IO.StreamWriter(filepath))
            {
                file.WriteLine("character counts, text to audio time");
            }
        }
        if (lastSavedIndices[3] < TimeRecvTTS.Count)
        {
            Debug.Log("tts time start from: " + lastSavedIndices[3] + " total: " + TimeRecvTTS.Count);
        }
        using (System.IO.StreamWriter file =
                       File.AppendText(filepath))
        {
            for (int i = lastSavedIndices[3]; i < TimeRecvTTS.Count; i++)
            {
                lastSavedIndices[3]++;
                file.WriteLine(CountTTS[i] + "," + (TimeRecvTTS[i] - TimeSendTTS[i]));
            }
        }
        List<float> TimeReact = audioControl.GetTimeReact();
        filename = "Time_data_react.csv";
        filepath = path + filename;
        if (!File.Exists(filepath))
        {
            using (System.IO.StreamWriter file =
                                                     new System.IO.StreamWriter(filepath))
            {
                file.WriteLine("reaction time");
            }
        }
        if (lastSavedIndices[4] < TimeReact.Count)
        {
            Debug.Log("reaction time start from: " + lastSavedIndices[4] + " total: " + TimeReact.Count);
        }
        using (System.IO.StreamWriter file =
                       File.AppendText(filepath))
        {
            for (int i = lastSavedIndices[4]; i < TimeReact.Count; i++)
            {
                lastSavedIndices[4]++;
                file.WriteLine(TimeReact[i]);
            }
        }
        Debug.Log("Data saved.");
    }

    public List<System.DateTime> GetResReqTime()
    {
        return res_req_time;
    }
    public List<System.DateTime> GetResEndTime()
    {
        return res_end_time;
    }
}
