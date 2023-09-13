using Unity.Networking.Transport;
using UnityEngine;

public class NetStartGame : NetMessages
{
    //Sending Message
    public NetStartGame()
    {
        Code = OpCode.START_GAME;
    }
    //Recieving Message
    public NetStartGame(DataStreamReader reader)
    {
        Code = OpCode.START_GAME;
        Deserialize(reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteByte((byte)Code);
    }
    public override void Deserialize(DataStreamReader reader)
    {
        
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_START_GAME?.Invoke(this);
    }
    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_START_GAME?.Invoke(this, cnn);
    }
}
