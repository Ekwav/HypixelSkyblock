namespace hypixel
{
    public class SubscribeCommand : Command
    {
        public override void Execute(MessageData data)
        {
            SubscribeEngine.Instance.Subscribe(data.GetAs<string>(),data.UserId);
            data.SendBack(data.Create("subscribeResponse","success"));
        }
    }
    public class UnsubscribeCommand : Command
    {
        public override void Execute(MessageData data)
        {
            SubscribeEngine.Instance.Unsubscribe(data.GetAs<string>(),data.UserId);
            data.SendBack(data.Create("unsubscribeResponse","unsubscribed"));
        }
    }
}