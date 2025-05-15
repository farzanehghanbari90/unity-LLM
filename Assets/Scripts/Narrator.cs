using UnityEngine;
//using UnityEngine.UI;
using TMPro;
using LMNT;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;

using UnityEngine.Networking;
using UnityEngine.UIElements;
using Unity.Properties;
using UnityEngine.Playables;

public class Narrator : MonoBehaviour
{
    //public TMP_Text narrationText;
    //public GameObject choicesContainer;
    //public GameObject[] cameraList;
    public PlayableDirector[] actDirectors;
    public class Data
    {
        public string narrationText;
    }

    Data data = new Data { narrationText = "" };


    private LMNTSpeech speech;
    private AudioSource audioSource;
    private int actIdx;
    private int introIdx;
    private int questionIdx;
    private bool talk;
    //private Button[] choiceButtons;
    private bool answered;
    private Queue<string> dialogueQueue = new Queue<string>();
    private string apiKey = "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";

    private HuggingFaceChatClient client;
    private bool llmTriggered;
    private string llmResponse;
    private float startTime;


    private enum State
    {
        ActInit,
        Intro,
        IntroWait,
        Question,
        QuestionWait,
        Answer,
        AnswerWait,
        LLM,
        LLMWait,
        Done,
    }

    private enum SoundState
    {
        Fetch,
        StartTalk,
        WaitTalk,
        WaitFinish,
    }
    private State state;
    private SoundState soundState;

    private class Act
    {
        public string[] Intro { get; set; }
        public Question[] Questions { get; set; }

    }

    private class Question
    {
        public string Text { get; set; }
        public string Context { get; set; }
        public string Answer
        {
            get; set;

        }
    }

    private class Narrative
    {
        public Act[] Acts;
    }
    private Narrative narrative;

    public void nextAct()
    {
        Debug.Log("next act");
    }
    public void Initialize()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        var label = root.Q<Label>("label-narration");
        root.dataSource = data;
        label.SetBinding("text", new DataBinding
        {
            dataSourcePath = new PropertyPath(nameof(Data.narrationText)),
            bindingMode = BindingMode.ToTarget
        });
        activateButtons(false);
        root.Q<Button>("button-yes").clickable.clicked += () => { OnButtonClick("yes"); };
        root.Q<Button>("button-no").clickable.clicked += () => { OnButtonClick("no"); };

    }

    private void activateButtons(bool visible)
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        root.Q<Button>("button-yes").visible = visible;
        root.Q<Button>("button-no").visible = visible;
    }

    void Start()
    {
        Initialize();

        narrative = new Narrative
        {
            Acts = new[]
            {
                //<b><color=#FFFF00>Character:</color></b>
                new Act
                {
                    Intro = new string[] {
                        "On a sunny morning, Fox, Rabbit, and Bear set off through the forest to visit Lion in his den. Fox led the way, Rabbit hopped excitedly, and Bear followed with a happy grumble, eager to see their wise friend. When they arrived, Lion greeted them warmly, inviting them inside for a visit."
                    },
                    Questions = new Question[] { }
                },
                new Act
                {
                    Intro = new string[] {
                        "Lion placed a steaming kettle on the wooden table, its whistle singing through the cozy den. Bear, Rabbit, and Fox settled around, cups ready, as the rich scent of wildberry tea filled the air"
                    },
                    Questions = new []
                    {
                        new Question 
                        { 
                            Answer="No", 
                            Text="Do you think the fox can see the kettle's spout?", 
                            Context="A round table with four animals sitting around it:\n  -**Lion (North)**\n  -**Rabbit (West)**.\n  -**Bear (East)**.\n  -**Fox (South)**.\n - A kettle sits in the middle of the table, its spout pointing toward the lion."
                        },
                        new Question 
                        {
                            Answer="Yes", 
                            Text="Do you think Rabbit can smell the tea?", 
                            Context="The warm, wildberry tea sits in the middle of the table, filling the air with its sweet scent. Rabbit, sitting to the left of the kettle, breathes in deeply."
                        },
                        // New question about the cake
                        new Question
                        {
                            Answer="Curious", 
                            Text="If the Lion brings a cake because it is his birthday, how do you think the other animals feel about it?", 
                            Context="Lion's birthday has arrived, and he proudly presents a cake to share. Fox, Rabbit, and Bear look at each other, exchanging expressions of surprise and curiosity, wondering how to celebrate this special occasion."
                        }
                    }
                },
            }
        };

        client = new HuggingFaceChatClient(apiKey);
        client.StartNewConversation();
        //choiceButtons = choicesContainer.GetComponentsInChildren<Button>(true);
        //foreach (Button btn in choiceButtons)
        //{
        //    btn.onClick.AddListener(() => OnButtonClick(btn));
        //}
        audioSource = GetComponent<AudioSource>();
        speech = GetComponent<LMNTSpeech>();
        actIdx = 0;
        state = State.ActInit;
        soundState = SoundState.Fetch;
    }

    void Update()
    {
        switch (state)
        {
            case State.ActInit:
                introIdx = -1;
                questionIdx = -1;
                talk = false;
                foreach (string intro in narrative.Acts[actIdx].Intro)
                    dialogueQueue.Enqueue(intro);
                //cameraList[actIdx].SetActive(true);
                actDirectors[actIdx].gameObject.SetActive(true);
                actDirectors[actIdx].Play();
                if (actIdx > 0)
                {
                    //    cameraList[actIdx - 1].SetActive(false);
                    actDirectors[actIdx - 1].gameObject.SetActive(false);
                }
                Step();
                break;
            case State.Intro:
                //narrationText.text = this.narrative.Acts[actIdx].Intro[introIdx];
                data.narrationText = this.narrative.Acts[actIdx].Intro[introIdx];
                talk = true;
                state = State.IntroWait;
                break;
            case State.IntroWait:
                if (!talk & !audioSource.isPlaying)
                {
                    Step();
                }
                break;
            case State.Question:
                dialogueQueue.Enqueue(this.narrative.Acts[actIdx].Questions[questionIdx].Text);
                //narrationText.text = this.narrative.Acts[actIdx].Questions[questionIdx].Text;
                data.narrationText = this.narrative.Acts[actIdx].Questions[questionIdx].Text;
                answered = false;
                talk = true;
                state = State.QuestionWait;
                break;
            case State.QuestionWait:
                if (!talk & !audioSource.isPlaying)
                {

                    state = State.Answer;
                }
                break;
            case State.Answer:
                activateButtons(true);
                startTime = Time.time;
                state = State.AnswerWait;
                break;
            case State.AnswerWait:
                if (answered)
                {
                    activateButtons(false);
                    //narrationText.text = "";
                    data.narrationText = "";
                    state = State.LLM;
                }
                break;
            case State.LLM:
                if (llmTriggered)
                {
                    //narrationText.text = llmResponse;
                    data.narrationText = llmResponse;
                    talk = true;
                    state = State.LLMWait;
                }
                break;
            case State.LLMWait:
                if (!talk & !audioSource.isPlaying)
                {
                    Step();
                }
                break;
            case State.Done:
                //narrationText.gameObject.SetActive(false);
                break;
        }
        switch (soundState)
        {
            case SoundState.Fetch:
                speech.dialogue = this.NextDialogue();
                if (speech.dialogue == "")
                    break;
                Debug.Log(speech.dialogue);
                StartCoroutine(speech.Prefetch());
                soundState = SoundState.StartTalk;
                break;
            case SoundState.StartTalk:
                if (talk)
                {
                    StartCoroutine(speech.Talk());
                    soundState = SoundState.WaitTalk;
                }
                break;
            case SoundState.WaitTalk:
                if (audioSource.isPlaying)
                {
                    talk = false;
                    soundState = SoundState.WaitFinish;
                }
                break;
            case SoundState.WaitFinish:
                if (!audioSource.isPlaying)
                    soundState = SoundState.Fetch;
                break;
        }
    }

    private void OnButtonClick(string text)
    {
        //TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        llmTriggered = false;
        GetAIResponse(client, BuildPrompt(text, Time.time - startTime));
        answered = true;
    }

    private string NextDialogue()
    {
        //if (this.actIdx == narrative.Acts.Length)
        //    return "None";
        //var act = narrative.Acts[this.actIdx];
        ////if (firstFetch)
        ////{
        ////    firstFetch = false;
        ////    return act.Intro[introIdx];
        ////}
        //if (introIdx >= act.Intro.Length)
        //{
        //    if (answered)
        //        return act.Questions[questionIdx];
        //    else
        //        return;
        //}
        //else
        //{
        //    return act.Intro[introIdx];
        //}
        if (dialogueQueue.Count == 0)
        {
            return "";
        }
        return dialogueQueue.Dequeue();
    }

    private void Step()
    {
        if (actIdx >= narrative.Acts.Length)
        {
            state = State.Done;
            return;
        }
        var act = narrative.Acts[actIdx];
        introIdx++;
        if (introIdx >= act.Intro.Length)
        {
            questionIdx++;
            if (questionIdx == act.Questions.Length)
            {
                actIdx++;
                if (actIdx == narrative.Acts.Length)
                {
                    state = State.Done;
                }
                else
                {
                    state = State.ActInit;
                }

            }
            else
            {
                state = State.Question;
            }
        }
        else
        {

            state = State.Intro;
        }
    }






    private string prompt =
    @"Role: You are a friendly and encouraging mentor/therapist for children (ages 4�8). Your goal is to guide the child through a learning game with patience, positivity, and simple language.
Scenario Context:
Current Scene: {0}
Question Asked: {1}
Correct Answer: {2}
Child�s Answer: {3}
Response Time: {4}
Task:
Assess the Answer:
If correct: Praise warmly and reinforce the concept.
If incorrect: Gently correct and encourage.
If response was slow: Add reassurance.
If response was fast: Celebrate confidence.
Therapeutic Tone:
Use simple words, and 1�2 sentences max.
Focus on growth mindset.
Output Format:
[Paragraph response for the child]";
    private string BuildPrompt(string answer, float responseTime)
    {
        Question question = narrative.Acts[actIdx].Questions[questionIdx];
        return string.Format(prompt, question.Context, question.Text, question.Answer, answer, $"{responseTime} seconds");
    }

    private async void GetAIResponse(HuggingFaceChatClient client, string prompt)
    {
        var response = await client.Prompt(
            model: "deepseek/deepseek-v3-0324",
            prompt: prompt);
        if (response != null)
        {
            dialogueQueue.Enqueue(response);
            llmResponse = response;
        }
        llmTriggered = true;
    }
    public class StoryPrompt
    {
        public string Story { get; set; }
        public string Question { get; set; }
        public string Option1 { get; set; }
        public string Option2 { get; set; }
        public static StoryPrompt ParseResponse(string llmResponse)
        {
            var prompt = new StoryPrompt();

            var storyMatch = Regex.Match(llmResponse, @"STORY:\s*(.+?)\s*QUESTION:");
            var questionMatch = Regex.Match(llmResponse, @"QUESTION:\s*(.+?)\s*OPTION 1:");
            var option1Match = Regex.Match(llmResponse, @"OPTION 1:\s*(.+?)\s*OPTION 2:");
            var option2Match = Regex.Match(llmResponse, @"OPTION 2:\s*(.+?)(\s*$|STORY:|QUESTION:|OPTION)");

            if (storyMatch.Success) prompt.Story = storyMatch.Groups[1].Value.Trim();
            if (questionMatch.Success) prompt.Question = questionMatch.Groups[1].Value.Trim();
            if (option1Match.Success) prompt.Option1 = option1Match.Groups[1].Value.Trim();
            if (option2Match.Success) prompt.Option2 = option2Match.Groups[1].Value.Trim();

            return prompt;
        }

        private async void GetAIResponse(HuggingFaceChatClient client, string prompt)
        {
            var response = await client.Prompt(
                model: "deepseek/deepseek-v3-0324",
                prompt: prompt);
            if (response != null)
            {

            }
        }






    }
    [System.Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    public class ChatRequest
    {
        public ChatMessage[] messages;
        public int max_tokens;
        public string model;
        public bool stream;
    }
    [System.Serializable]
    public class ChatCompletionResponse
    {
        public Choice[] choices;
        public int created;
        public string id;
        public string model;
        public string @object;
        public Usage usage;
    }

    [System.Serializable]
    public class Choice
    {
        public Message message;
        public int index;
        public string finish_reason;
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    public class Usage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
    }

    public class HuggingFaceChatClient
    {
        private readonly string _apiKey;
        private List<ChatMessage> _messages;

        public HuggingFaceChatClient(string apiKey)
        {
            _apiKey = apiKey;
            _messages = new List<ChatMessage>();
        }


        public void StartNewConversation()
        {
            _messages.Clear();
        }
        public async Task<string> Prompt(string model, string prompt, int maxTokens = 500, bool stream = false)
        {
            _messages.Add(new ChatMessage { role = "user", content = prompt });
            var requestBody = new ChatRequest
            {
                messages = _messages.ToArray(),
                max_tokens = maxTokens,
                model = model,
                stream = stream
            };
            UnityWebRequest webRequest = new UnityWebRequest("https://router.huggingface.co/novita/v3/openai/chat/completions", "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestBody));
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

            await webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = webRequest.downloadHandler.text;
                string response = JsonUtility.FromJson<ChatCompletionResponse>(jsonResponse).choices[0].message.content;
                _messages.Add(new ChatMessage { role = "assistant", content = response });
                return response;
            }
            else
            {
                Debug.LogError("Error: " + webRequest.error);
                return null;
            }
        }
    }
}