using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Steamworks;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2 || (args[0] != "--check" && args[0] != "--verify" && args[0] != "--publish"))
        {
            Console.Error.WriteLine("Usage: WorkshopPublisher --check|--verify|--publish workshop.vdf");
            return 1;
        }

        try
        {
            VdfNode config = VdfReader.Read(File.ReadAllText(args[1], Encoding.UTF8)).Blocks["workshopitem"];
            uint appId = uint.Parse(config.Values["appid"]);
            PublishedFileId_t itemId = new PublishedFileId_t(ulong.Parse(config.Values["publishedfileid"]));
            VdfNode translations = config.Blocks["localizations"];
            foreach (KeyValuePair<string, VdfNode> pair in translations.Blocks)
            {
                Console.WriteLine(pair.Key + ": " + pair.Value.Values["title"]
                    + " (" + Encoding.UTF8.GetByteCount(pair.Value.Values["description"]) + " bytes)");
            }
            if (args[0] == "--check") return 0;

            // SteamAPI.InitEx reports missing/running-client and interface initialization errors.
            Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
            string error;
            ESteamAPIInitResult init = SteamAPI.InitEx(out error);
            if (init != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                throw new InvalidOperationException("Steam initialization: " + init + ": " + error);

            try
            {
                // 2026-11-17: desktop connection_log records Session Replaced after SteamCMD login.
                // InitEx can still succeed through local IPC while this live connection is absent.
                SteamAPI.RunCallbacks();
                bool connected = SteamUser.BLoggedOn();
                Console.WriteLine("Desktop Steam connected: " + connected);
                Require(connected, "Desktop Steam is disconnected. Quit SteamCMD, reconnect the desktop Steam client, then run this script again.");
                if (args[0] == "--publish")
                {
                    foreach (KeyValuePair<string, VdfNode> pair in translations.Blocks)
                        Publish(appId, itemId, pair.Key, pair.Value, config);
                }
                foreach (KeyValuePair<string, VdfNode> pair in translations.Blocks)
                {
                    Verify(itemId, pair.Key, pair.Value, config, args[0] == "--publish");
                }
            }
            finally
            {
                SteamAPI.Shutdown();
            }
            return 0;
        }
        catch (Exception error)
        {
            // CLI failure must reach the calling script; never report a partial upload as success.
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static void Publish(uint appId, PublishedFileId_t itemId, string language, VdfNode text, VdfNode config)
    {
        UGCUpdateHandle_t update = SteamUGC.StartItemUpdate(new AppId_t(appId), itemId);
        Require(SteamUGC.SetItemUpdateLanguage(update, language), "SetItemUpdateLanguage");
        Require(SteamUGC.SetItemTitle(update, text.Values["title"]), "SetItemTitle");
        Require(SteamUGC.SetItemDescription(update, text.Values["description"]), "SetItemDescription");
        Require(SteamUGC.SetItemTags(update, new List<string>(config.Blocks["tags"].Values.Values)), "SetItemTags");
        SubmitItemUpdateResult_t result = Wait<SubmitItemUpdateResult_t>(SteamUGC.SubmitItemUpdate(update, ""));
        Require(result.m_eResult == EResult.k_EResultOK, "SubmitItemUpdate: " + result.m_eResult);
        // SubmitItemUpdateResult_t explicitly exposes a mandatory Workshop agreement state.
        Require(!result.m_bUserNeedsToAcceptWorkshopLegalAgreement, "Steam requires the account owner to accept the Workshop agreement.");
        Console.WriteLine("Updated " + language + " for item " + itemId.m_PublishedFileId);
    }

    private static void Verify(PublishedFileId_t itemId, string language, VdfNode text, VdfNode config, bool fresh)
    {
        UGCQueryHandle_t query = SteamUGC.CreateQueryUGCDetailsRequest(new[] { itemId }, 1);
        try
        {
            Require(SteamUGC.SetLanguage(query, language), "SetLanguage");
            Require(SteamUGC.SetReturnLongDescription(query, true), "SetReturnLongDescription");
            if (fresh) Require(SteamUGC.SetAllowCachedResponse(query, 0), "SetAllowCachedResponse");
            SteamUGCQueryCompleted_t result = Wait<SteamUGCQueryCompleted_t>(SteamUGC.SendQueryUGCRequest(query));
            Require(result.m_eResult == EResult.k_EResultOK, "Workshop query: " + result.m_eResult);
            SteamUGCDetails_t details;
            Require(SteamUGC.GetQueryUGCResult(query, 0, out details), "GetQueryUGCResult");
            Require(details.m_eResult == EResult.k_EResultOK, "Item query: " + details.m_eResult);
            Console.WriteLine(language + " remote title: " + details.m_rgchTitle);
            bool sameText = details.m_rgchTitle == text.Values["title"]
                && details.m_rgchDescription.Replace("\r\n", "\n") == text.Values["description"].Replace("\r\n", "\n");
            Console.WriteLine(language + " description matches: " + sameText);
            Console.WriteLine("Remote tags: " + details.m_rgchTags);
            if (fresh)
            {
                Require(sameText, "Remote " + language + " text differs from VDF.");
                List<string> remoteTags = new List<string>(details.m_rgchTags.Split(','));
                foreach (string tag in config.Blocks["tags"].Values.Values)
                    Require(remoteTags.Contains(tag), "Remote tag missing: " + tag);
            }
        }
        finally
        {
            SteamUGC.ReleaseQueryUGCRequest(query);
        }
    }

    private static T Wait<T>(SteamAPICall_t call)
    {
        Require(call != SteamAPICall_t.Invalid, "Steam returned an invalid asynchronous call.");
        T result = default(T);
        bool done = false;
        bool failed = false;
        using (CallResult<T> pending = CallResult<T>.Create(delegate(T value, bool ioFailure)
        {
            result = value;
            failed = ioFailure;
            done = true;
        }))
        {
            pending.Set(call);
            Stopwatch watch = Stopwatch.StartNew();
            while (!done)
            {
                SteamAPI.RunCallbacks();
                if (watch.Elapsed.TotalSeconds > 120)
                    throw new TimeoutException("Steam callback timed out; query the remote item before repeating publication.");
                Thread.Sleep(50);
            }
        }
        Require(!failed, "Steam reported asynchronous I/O failure.");
        return result;
    }

    private static void Require(bool success, string operation)
    {
        if (!success) throw new InvalidOperationException(operation);
    }
}

internal sealed class VdfNode
{
    internal readonly Dictionary<string, string> Values = new Dictionary<string, string>();
    internal readonly Dictionary<string, VdfNode> Blocks = new Dictionary<string, VdfNode>();
}

internal sealed class VdfReader
{
    private readonly MatchCollection tokens;
    private int index;

    private VdfReader(string source)
    {
        tokens = Regex.Matches(source, "\"(?:\\\\.|[^\"\\\\])*\"|[{}]");
    }

    internal static VdfNode Read(string source)
    {
        return new VdfReader(source).ReadNode(false);
    }

    private VdfNode ReadNode(bool nested)
    {
        VdfNode node = new VdfNode();
        while (index < tokens.Count)
        {
            string key = tokens[index++].Value;
            if (key == "}")
            {
                if (!nested) throw new FormatException("Unexpected VDF closing brace.");
                return node;
            }
            string value = tokens[index++].Value;
            if (value == "{") node.Blocks.Add(Unquote(key), ReadNode(true));
            else node.Values.Add(Unquote(key), Unquote(value));
        }
        if (nested) throw new FormatException("Missing VDF closing brace.");
        return node;
    }

    private static string Unquote(string token)
    {
        if (!token.StartsWith("\"") || !token.EndsWith("\"")) throw new FormatException("Expected quoted VDF string.");
        return token.Substring(1, token.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
    }
}
