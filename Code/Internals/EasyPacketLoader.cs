/*
 *  EasyPacketLoader.cs
 *  DavidFDev
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace EasyPacketsLib.Internals;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class EasyPacketLoader : ModSystem
{
    #region Static Fields and Constants

    private static readonly Dictionary<ushort, IEasyPacket> PacketByNetId = [];
    private static readonly Dictionary<IntPtr, ushort> NetIdByPtr = [];
    private static readonly HashSet<Mod> RegisteredMods = [];
    private static readonly string EasyPacketFullName;

    #endregion

    #region Static Methods

    /// <summary>
    ///     Check if an easy packet is registered.
    /// </summary>
    public static bool IsRegistered(in IEasyPacket packet)
    {
        return NetIdByPtr.ContainsKey(packet.GetType().TypeHandle.Value);
    }

    /// <summary>
    ///     Get an easy packet type by its registered net ID.
    /// </summary>
    public static IEasyPacket GetPacket(ushort netId)
    {
        return PacketByNetId.GetValueOrDefault(netId);
    }

    /// <summary>
    ///     Get the registered net ID of an easy packet.
    /// </summary>
    public static ushort GetNetId(in IEasyPacket packet)
    {
        return NetIdByPtr.GetValueOrDefault(packet.GetType().TypeHandle.Value);
    }

    /// <summary>
    ///     Register easy packets and handlers of the provided mod.
    /// </summary>
    public static void RegisterMod(Mod mod)
    {
        if (!RegisteredMods.Add(mod))
        {
            // Already registered
            return;
        }

        // The interface is checked by name (not type), so we must also check which assembly it is defined in
        var assembly = Assembly.GetExecutingAssembly();

        // Register easy packets
        var loadableTypes = AssemblyManager.GetLoadableTypes(mod.Code);
        foreach (var type in loadableTypes
                     .Where(t => t.IsValueType && !t.ContainsGenericParameters && t.GetInterface(EasyPacketFullName)?.Assembly == assembly)
                     .OrderBy(t => t.FullName, StringComparer.InvariantCulture))
        {
            RegisterPacket(mod, type);
        }
    }

    /// <summary>
    ///     Clear static references when the mod is unloaded.
    /// </summary>
    public static void ClearStatics()
    {
        PacketByNetId.Clear();
        NetIdByPtr.Clear();
        RegisteredMods.Clear();
        NetEasyPacketCount = 0;
    }

    /// <summary>
    ///     Register an easy packet.
    /// </summary>
    /// <param name="mod">Mod that defined the easy packet.</param>
    /// <param name="type">Type that implements <see cref="IEasyPacket" />.</param>
    private static void RegisterPacket(Mod mod, Type type)
    {
        // Create a new default instance of the easy packet type
        // https://stackoverflow.com/a/1151470/20943906
        var instance = (IEasyPacket)Activator.CreateInstance(type, true) ??
            throw new Exception($"Failed to register easy packet type: {type.Name}.");

        // Register the created instance, assigning a unique net id
        var netId = NetEasyPacketCount++;
        PacketByNetId.Add(netId, instance);
        NetIdByPtr.Add(type.TypeHandle.Value, netId);

        mod.Logger.Debug($"Registered IEasyPacket<{type.Name}> (Mod: {mod.Name}, ID: {netId})");
    }

    #endregion

    #region Constructors

    static EasyPacketLoader()
    {
        // Cache the full interface type definitions to be used during loading
        EasyPacketFullName = typeof(IEasyPacket).FullName;
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Total number of easy packets registered across all registered mods.
    /// </summary>
    public static ushort NetEasyPacketCount { get; private set; }

    #endregion

    #region Methods

    public override void Load()
    {
        // Register loaded mods; order must be the same for all users, so that net ids are synced
        foreach (var mod in ModLoader.Mods.Where(static m => m.Side is ModSide.Both).OrderBy(static m => m.Name, StringComparer.InvariantCulture))
        {
            RegisterMod(mod);
        }
    }

    public override void Unload()
    {
        // Ensure the static fields are cleared
        ClearStatics();
    }

    #endregion
}