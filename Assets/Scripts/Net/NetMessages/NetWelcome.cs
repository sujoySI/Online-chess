using Unity.Networking.Transport;
using UnityEngine;

public class NetWelcome : NetMessages
{
    public int AssignedTeam { get; set; }
    
    //Sending Message
    public NetWelcome()
    {
        Code = OpCode.WELCOME;
    }
    //Recieving Message
    public NetWelcome(DataStreamReader reader)
    {
        Code = OpCode.WELCOME;
        Deserialize(reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteByte((byte)Code);
        writer.WriteInt(AssignedTeam);
    }
    public override void Deserialize(DataStreamReader reader)
    {
        //We already read the byte in the NetUtility::OnData
        AssignedTeam = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_WELCOME?.Invoke(this);
    }
    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_WELCOME?.Invoke(this, cnn);
    }
}
