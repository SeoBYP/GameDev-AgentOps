using System.Collections.Generic;

namespace AgentOps.Sessions
{
    public class AgentSession
    {
        private string sessionId;
        private string model;
        private List<ChatMessage> messages;

        public AgentSession(string sessionId, string model)
        {
            this.sessionId = sessionId;
            this.model = model;
            this.messages = new List<ChatMessage>();
        }
        
        public void AddMessage(string role, string content)
            => messages.Add(new ChatMessage { role = role, content = content });
        
        public void AddMessage(ChatMessage message)
            => messages.Add(message);
        
        public string GetSessionId()
            => sessionId;
        
        public string GetModel()
            => model;
        
        public List<ChatMessage> GetMessages()
            => messages;
        
        public void ClearMessages()
            => messages.Clear();
    }
}