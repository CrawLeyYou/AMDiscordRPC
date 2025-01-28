using DiscordRPC;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC
{
    public class Discord
    {
        public static void InitializeDiscordRPC()
        {
            client = new DiscordRpcClient("1308911584164319282");
            client.Initialize();
        }
    }
}
