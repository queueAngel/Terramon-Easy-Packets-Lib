/*
 *  ExamplePacket.cs
 *  DavidFDev
 */

using EasyPacketsLib.Internals;
using Microsoft.Xna.Framework;
using System.IO;
using Terraria;
using Terraria.Chat;
using Terraria.Localization;
using Terraria.ModLoader;

namespace EasyPacketsLib.Examples;

internal struct ExamplePacket(int x, int y) : IEasyPacket
{
    #region Fields

    public int X = x;
    public int Y = y;

    #endregion
    #region Constructors

    #endregion

    #region Methods

    readonly void IEasyPacket.Serialise(BinaryWriter writer)
    {
        writer.Write(X);
        writer.Write(Y);
    }

    void IEasyPacket.Deserialise(BinaryReader reader, in SenderInfo sender)
    {
        X = reader.ReadInt32();
        Y = reader.ReadInt32();
    }

    readonly void IEasyPacket.Receive(in SenderInfo sender, ref bool handled)
    {
        ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral($"{nameof(ExamplePacket)}: Received example packet from {sender.WhoAmI}: ({X}, {Y})."), Color.White);
        handled = true;
    }

    #endregion
}

// ReSharper disable once UnusedType.Global
internal sealed class ExamplePacketCommand : ModCommand
{
    #region Properties

    public override string Command => "expacket";

    public override CommandType Type => CommandType.Chat;

    #endregion

    #region Methods

    public override bool IsLoadingEnabled(Mod mod)
    {
#if RELEASE
        return false;
#else
        return true;
#endif
    }

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral($"Sending example packet from {Main.myPlayer}."), Color.White);
        Mod.SendPacket(new ExamplePacket(10, 25));
    }

    #endregion
}