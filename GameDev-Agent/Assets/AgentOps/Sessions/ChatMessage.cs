namespace AgentOps.Sessions
{
    [System.Serializable]
    public class ChatMessage
    {
        public string role;     // "user" | "assistant"
        public string content;
    }

}