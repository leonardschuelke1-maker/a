using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Leauge_Auto_Accept
{
    internal class MainLogic
    {
        public static bool isAutoAcceptOn = false;

        private static bool pickedChamp = false;
        private static bool lockedChamp = false;
        private static bool pickedBan = false;
        private static bool lockedBan = false;
        private static bool pickedSpell1 = false;
        private static bool pickedSpell2 = false;
        private static bool sentChatMessages = false;

        // Arena
        private static bool isArena = false;
        private static string crowdFavorite1ChampId = "";
        private static string crowdFavorite2ChampId = "";
        private static string crowdFavorite3ChampId = "";
        private static string crowdFavorite4ChampId = "";
        private static string crowdFavorite5ChampId = "";

        private static long champSelectStart;
        private static string lastChatRoom = "";

        private static long queueStartTime;
        private static string lastPhase = "";

        public static void acceptQueue()
        {
            while (true)
            {
                if (isAutoAcceptOn)
                {
                    string[] gameSession = LCU.clientRequest("GET", "lol-gameflow/v1/session");

                    if (gameSession[0] == "200")
                    {
                        string phase = gameSession[1].Split("phase").Last().Split('"')[2];

                        if (Settings.autoRestartQueue)
                        {
                            handleQueueRestart(phase);
                        }

                        switch (phase)
                        {
                            case "Lobby":
                                Thread.Sleep(5000);
                                break;
                            case "Matchmaking":
                                handleMatchmakingCancel();
                                Thread.Sleep(2000);
                                break;
                            case "ReadyCheck":
                                handleMatchmakingAccept();
                                break;
                            case "ChampSelect":
                                handleChampSelect();
                                handlePickOrderSwap();
                                break;
                            case "InProgress":
                                Thread.Sleep(9000);
                                break;
                            case "WaitingForStats":
                                Thread.Sleep(9000);
                                break;
                            case "PreEndOfGame":
                                Thread.Sleep(9000);
                                break;
                            case "EndOfGame":
                                Thread.Sleep(5000);
                                break;
                            default:
                                Thread.Sleep(1000);
                                break;
                        }

                        if (phase != "ChampSelect")
                        {
                            lastChatRoom = "";
                        }
                    }
                    Thread.Sleep(50);
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }
        }

        private static void handleMatchmakingCancel()
        {
            if (!Settings.cancelQueueAfterDodge)
            {
                return;
            }
            if (lastChatRoom != "")
            {
                LCU.clientRequest("DELETE", "lol-lobby/v2/lobby/matchmaking/search");
                lastChatRoom = "";
            }
        }

        private static void handleMatchmakingAccept()
        {
            if (!Settings.cancelQueueAfterDodge)
            {
                LCU.clientRequest("POST", "lol-matchmaking/v1/ready-check/accept");
                return;
            }
            else
            {
                if (lastChatRoom != "")
                {
                    LCU.clientRequest("POST", "lol-matchmaking/v1/ready-check/decline");
                    lastChatRoom = "";
                }
                else
                {
                    LCU.clientRequest("POST", "lol-matchmaking/v1/ready-check/accept");
                }
            }
        }

        private static void handleQueueRestart(string phase)
        {
            if (phase == "Matchmaking" && phase != lastPhase)
            {
                queueStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
            else if (phase == "Matchmaking")
            {
                long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if ((currentTime - Settings.queueMaxTime) > queueStartTime)
                {
                    LCU.clientRequest("DELETE", "lol-lobby/v2/lobby/matchmaking/search");
                    LCU.clientRequest("POST", "lol-lobby/v2/lobby/matchmaking/search");
                    queueStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                }
            }
            lastPhase = phase;
        }

        private static void handleChampSelect()
        {
            string[] currentChampSelect = LCU.clientRequest("GET", "lol-champ-select/v1/session");

            if (currentChampSelect[0] == "200")
            {
                string currentChatRoom = currentChampSelect[1].Split("multiUserChatId\":\"")[1].Split('"')[0];
                if (lastChatRoom != currentChatRoom || lastChatRoom == "")
                {
                    pickedChamp = false;
                    lockedChamp = false;
                    pickedBan = false;
                    lockedBan = false;
                    pickedSpell1 = false;
                    pickedSpell2 = false;
                    sentChatMessages = false;
                    champSelectStart = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    
                    isArena = currentChampSelect[1].Contains("\"queueId\":1700");
                    crowdFavorite1ChampId = "";
                    crowdFavorite2ChampId = "";
                    crowdFavorite3ChampId = "";
                    crowdFavorite4ChampId = "";
                    crowdFavorite5ChampId = "";
                }
                lastChatRoom = currentChatRoom;

                if (pickedChamp && lockedChamp && pickedBan && lockedBan && pickedSpell1 && pickedSpell2 && sentChatMessages)
                {
                    Thread.Sleep(1000);
                }
                else
                {
                    string localPlayerCellId = currentChampSelect[1].Split("localPlayerCellId\":")[1].Split(',')[0];

                    if (Settings.currentChamp[1] == "0" && !isArena)
                    {
                        pickedChamp = true;
                        lockedChamp = true;
                    }
                    if (Settings.currentBan[1] == "0")
                    {
                        pickedBan = true;
                        lockedBan = true;
                    }
                    if (Settings.currentSpell1[1] == "0")
                    {
                        pickedSpell1 = true;
                    }
                    if (Settings.currentSpell2[1] == "0")
                    {
                        pickedSpell2 = true;
                    }
                    if (!Settings.chatMessagesEnabled || Settings.chatMessages.Count == 0)
                    {
                        sentChatMessages = true;
                    }

                    if (isArena)
                    {
                        // Endpoint mit Typo-Fallback abfragen
                        string[] arenaCrowdFavorites = LCU.clientRequest("GET", "lol-lobby-team-builder/champ-select/v1/crowd-favorte-champion-list");
                        if (arenaCrowdFavorites[0] != "200")
                        {
                            arenaCrowdFavorites = LCU.clientRequest("GET", "lol-lobby-team-builder/champ-select/v1/crowd-favorite-champion-list");
                        }

                        if (arenaCrowdFavorites[0] == "200")
                        {
                            string arenaCrowdFavoritesData = arenaCrowdFavorites[1].Replace("[", "").Replace("]", "").Replace("\n", "").Replace(" ", "");
                            string[] arenaCrowdFavoritesSplit = arenaCrowdFavoritesData.Split(',');

                            if (arenaCrowdFavoritesSplit.Length > 0) crowdFavorite1ChampId = arenaCrowdFavoritesSplit[0];
                            if (arenaCrowdFavoritesSplit.Length > 1) crowdFavorite2ChampId = arenaCrowdFavoritesSplit[1];
                            if (arenaCrowdFavoritesSplit.Length > 2) crowdFavorite3ChampId = arenaCrowdFavoritesSplit[2];
                            if (arenaCrowdFavoritesSplit.Length > 3) crowdFavorite4ChampId = arenaCrowdFavoritesSplit[3];
                            if (arenaCrowdFavoritesSplit.Length > 4) crowdFavorite5ChampId = arenaCrowdFavoritesSplit[4];
                        }
                    }
                    
                    if (isArena || !pickedChamp || !lockedChamp || !pickedBan || !lockedBan)
                    {
                        handleChampSelectActions(currentChampSelect, localPlayerCellId);
                    }
                    if (!sentChatMessages)
                    {
                        handleChampSelectChat(currentChatRoom);
                    }
                    if (!pickedSpell1)
                    {
                        string[] champSelectAction = LCU.clientRequest("PATCH", "lol-champ-select/v1/session/my-selection", "{\"spell1Id\":" + Settings.currentSpell1[1] + "}");
                        if (champSelectAction[0] == "204") pickedSpell1 = true;
                    }
                    if (!pickedSpell2)
                    {
                        string[] champSelectAction = LCU.clientRequest("PATCH", "lol-champ-select/v1/session/my-selection", "{\"spell2Id\":" + Settings.currentSpell2[1] + "}");
                        if (champSelectAction[0] == "204") pickedSpell2 = true;
                    }
                }
            }
        }

        private static bool handleChampPositionPreferences(string[] currentChampSelect, string localPlayerCellId)
        {
            string[] lobbySession = LCU.clientRequest("GET", "lol-lobby/v2/lobby");
            if (!lobbySession[1].Contains("firstPositionPreference\":") || !lobbySession[1].Contains("secondPositionPreference\":")) return true;

            string firstPositionPreference = lobbySession[1].Split("firstPositionPreference\":")[1].Split(',')[0].Trim('"').ToLower();
            string secondPositionPreference = lobbySession[1].Split("secondPositionPreference\":")[1].Split(',')[0].Trim('"').ToLower();

            int startIndex = currentChampSelect[1].IndexOf("\"myTeam\":[");
            if (startIndex == -1) return true;

            int teamDataStart = currentChampSelect[1].IndexOf("[{", startIndex);
            int teamDataEnd = currentChampSelect[1].IndexOf("}]", teamDataStart);
            if (teamDataStart == -1 || teamDataEnd == -1) return true;
            
            string myTeam = currentChampSelect[1].Substring(teamDataStart + 1, teamDataEnd - teamDataStart);
            string[] players = myTeam.Split(new string[] { "{" }, StringSplitOptions.RemoveEmptyEntries);

            string assignedPosition = "";
            foreach (var player in players)
            {
                if (player.Contains("\"cellId\":" + localPlayerCellId + ","))
                {
                    string[] lines = player.Split(',');
                    foreach (var line in lines)
                    {
                        if (line.Contains("\"assignedPosition\""))
                        {
                            string[] parts = line.Split(':');
                            assignedPosition = parts[1].Trim('"');
                        }
                    }
                }
            }
            
            if (firstPositionPreference == assignedPosition) return true;
            if (secondPositionPreference == assignedPosition) return false;
            
            return true;
        }

        private static void handleChampSelectChat(string chatId)
        {
            string[] chats = LCU.clientRequest("GET", "lol-chat/v1/conversations", "");
            if (chats[1].Contains(chatId))
            {
                Data.loadPlayerChatId();
                long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                if ((currentTime - Settings.chatMessagesDelay) > champSelectStart)
                {
                    handleChampSelectChatSendMsg(chatId);
                }
            }
        }

        private static void handleChampSelectChatSendMsg(string chatId)
        {
            foreach (var message in Settings.chatMessages)
            {
                int attempts = 0;
                string httpRes = "";
                while (httpRes != "200" && attempts < 3)
                {
                    string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    string body = "{\"type\":\"chat\",\"fromId\":\"" + Data.currentChatId + "\",\"fromSummonerId\":" + Data.currentSummonerId + ",\"isHistorical\":false,\"timestamp\":\"" + timestamp + "\",\"body\":\"" + message + "\"}";
                    string[] response = LCU.clientRequest("POST", "lol-chat/v1/conversations/" + chatId + "/messages", body);
                    attempts++;
                    httpRes = response[0];
                    Thread.Sleep(attempts * 20);
                }
            }
            sentChatMessages = true;
        }

        private static void handleChampSelectActions(string[] currentChampSelect, string localPlayerCellId)
        {
            if (!currentChampSelect[1].Contains("actions\":[[{")) return;

            string csActs = currentChampSelect[1].Split("actions\":[[{")[1].Split("}]],")[0];
            csActs = csActs.Replace("}],[{", "},{");
            string[] csActsArr = csActs.Split("},{");

            foreach (var act in csActsArr)
            {
                string ActCctorCellId = act.Split("actorCellId\":")[1].Split(',')[0];
                string ActCompleted = act.Split("completed\":")[1].Split(',')[0];
                string ActType = act.Split("type\":\"")[1].Split('"')[0];
                string championId = act.Split("championId\":")[1].Split(',')[0];
                string actId = act.Split(",\"id\":")[1].Split(',')[0];
                string ActIsInProgress = act.Split("isInProgress\":")[1].Split(',')[0];

                if (ActCctorCellId == localPlayerCellId && ActCompleted == "false" && ActType == "pick")
                {
                    bool usePrimaryChamp = handleChampPositionPreferences(currentChampSelect, localPlayerCellId);
                    handlePickAction(actId, championId, ActIsInProgress, currentChampSelect, usePrimaryChamp);
                }
                else if (ActCctorCellId == localPlayerCellId && ActCompleted == "false" && ActType == "ban")
                {
                    handleBanAction(actId, championId, ActIsInProgress, currentChampSelect);
                }
            }
        }

        private static bool ShouldHoverChampion(string[] currentChampSelect)
        {
            string champSelectPhase = currentChampSelect[1].Split("\"phase\":\"")[1].Split('"')[0];
            long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            return (currentTime - Settings.pickStartHoverDelay) > champSelectStart
                   || champSelectPhase != "PLANNING"
                   || Settings.instantHover;
        }

        private static bool isInCrowdFavoriteChamps(string champId) =>
            champId == crowdFavorite1ChampId ||
            champId == crowdFavorite2ChampId ||
            champId == crowdFavorite3ChampId ||
            champId == crowdFavorite4ChampId ||
            champId == crowdFavorite5ChampId;

        private static void handlePickAction(string actId, string championId, string ActIsInProgress, string[] currentChampSelect, bool usePrimaryChamp)
        {
            if (championId == "0") pickedChamp = false;

            if (!pickedChamp && ShouldHoverChampion(currentChampSelect))
            {
                if (isArena)
                {
                    // 1. BRAVERY: Nimm direkt den ersten verfügbaren Champion aus der Riot-Favoritenliste
                    if (Settings.bravery)
                    {
                        string[] crowdFavs = {
                            crowdFavorite1ChampId,
                            crowdFavorite2ChampId,
                            crowdFavorite3ChampId,
                            crowdFavorite4ChampId,
                            crowdFavorite5ChampId
                        };

                        foreach (var favId in crowdFavs)
                        {
                            if (!string.IsNullOrEmpty(favId) && favId != "0" && !pickedChamp)
                            {
                                hoverChampion(actId, favId, "pick");
                                if (pickedChamp) championId = favId;
                            }
                        }
                    }

                    // 2. MANUELLE FAVORITEN: Nutze diese, wenn Bravery aus ist ODER fehlschlug
                    if (!pickedChamp)
                    {
                        string[] customFavorites = {
                            Settings.crowdFavouraiteChamp1[1],
                            Settings.crowdFavouraiteChamp2[1],
                            Settings.crowdFavouraiteChamp3[1],
                            Settings.crowdFavouraiteChamp4[1],
                            Settings.crowdFavouraiteChamp5[1]
                        };

                        foreach (var favId in customFavorites)
                        {
                            if (!pickedChamp && favId != "0" && isInCrowdFavoriteChamps(favId))
                            {
                                hoverChampion(actId, favId, "pick");
                                if (pickedChamp) championId = favId;
                            }
                        }
                    }

                    pickedSpell1 = pickedSpell2 = true;
                }

                // Standard Pick für Draft/Ranked Mode
                if (!isArena && !pickedChamp && championId != "-3")
                {
                    string primaryChamp = usePrimaryChamp ? Settings.currentChamp[1] : Settings.secondaryChamp[1];
                    string primaryRunes = usePrimaryChamp ? Settings.currentChampRunes[1] : Settings.secondaryChampRunes[1];
                    string backupChamp = usePrimaryChamp ? Settings.currentBackupChamp[1] : Settings.secondaryBackupChamp[1];
                    string backupRunes = usePrimaryChamp ? Settings.currentBackupChampRunes[1] : Settings.secondaryBackupChampRunes[1];

                    hoverChampion(actId, primaryChamp, "pick");
                    handleRunes(primaryRunes);

                    if (!pickedChamp)
                    {
                        hoverChampion(actId, backupChamp, "pick");
                        handleRunes(backupRunes);
                        if (pickedChamp) championId = backupChamp;
                    } 
                    else championId = primaryChamp;
                }
            }

            if (ActIsInProgress == "true")
            {
                Debug.WriteLine($"ActIsInProgress: true | pickedChamp: {pickedChamp}, lockedChamp: {lockedChamp}, championId: {championId}");

                if (!lockedChamp && pickedChamp)
                {
                    // Im Arena-Modus direkt locken, um Timer-Fehler in checkLockDelay zu vermeiden
                    if (isArena || Settings.instaLock) lockChampion(actId, championId, "pick");
                    else checkLockDelay(actId, championId, currentChampSelect, "pick");
                }
            }
        }

        private static void handleBanAction(string actId, string championId, string ActIsInProgress, string[] currentChampSelect)
        {
            string champSelectPhase = currentChampSelect[1].Split("\"phase\":\"")[1].Split('"')[0];

            if (ActIsInProgress == "true" && champSelectPhase != "PLANNING")
            {
                if (!pickedBan)
                {
                    long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                    if (currentTime - Settings.banStartHoverDelay > champSelectStart)
                    {
                        bool dontBanCrowd = isArena && Settings.banCrowdFavourite && isInCrowdFavoriteChamps(Settings.currentBan[1]);
                        hoverChampion(actId, dontBanCrowd ? "0" : Settings.currentBan[1], "ban");
                    }
                }

                if (!lockedBan)
                {
                    if (!Settings.instaBan || isArena)
                    {
                        if (isArena) lockChampion(actId, championId, "ban");
                        else checkLockDelay(actId, championId, currentChampSelect, "ban");
                    }
                    else
                    {
                        lockChampion(actId, championId, "ban");
                    }
                }
            }
        }

        private static void hoverChampion(string actId, string currentChamp, string actType)
        {
            string[] champSelectAction = LCU.clientRequest("PATCH", "lol-champ-select/v1/session/actions/" + actId, "{\"championId\":" + currentChamp + "}");
            if (champSelectAction[0] == "204")
            {
                if (actType == "pick")
                {
                    pickedChamp = true;
                }
                else if (actType == "ban")
                {
                    pickedBan = true;
                }
            }
        }

        private static void handleRunes(string currentRunes)
        {
            LCU.clientRequest("PUT", "lol-perks/v1/currentpage", currentRunes);
        }

        private static void lockChampion(string actId, string championId, string actType)
        {
            string[] champSelectAction = LCU.clientRequest("PATCH", "lol-champ-select/v1/session/actions/" + actId, "{\"completed\":true,\"championId\":" + championId + "}");
            if (champSelectAction[0] == "204")
            {
                if (actType == "pick")
                {
                    lockedChamp = true;
                }
                else if (actType == "ban")
                {
                    lockedBan = true;
                }
            }
        }

        private static void checkLockDelay(string actId, string championId, string[] currentChampSelect, string actType)
        {
            long totalTime = Convert.ToInt64(currentChampSelect[1].Split("totalTimeInPhase\":")[1].Split(",")[0].Split("}")[0]);
            long remaining = Convert.ToInt64(currentChampSelect[1].Split("adjustedTimeLeftInPhase\":")[1].Split(",")[0].Split("}")[0]);
            long elapsed = totalTime - remaining;

            int startDelay = actType == "pick" ? Settings.pickStartlockDelay : Settings.banStartlockDelay;
            int endDelay = actType == "pick" ? Settings.pickEndlockDelay : Settings.banEndlockDelay;

            if (remaining <= endDelay || elapsed >= startDelay)
            {
                lockChampion(actId, championId, actType);
            }
        }

        private static void handlePickOrderSwap()
        {
            if (!Settings.autoPickOrderTrade || lockedChamp)
            {
                return;
            }

            string[] swap = LCU.clientRequest("GET", "lol-champ-select/v1/ongoing-swap");
            if (swap[0] == "200")
            {
                if (swap.Contains("initiatedByLocalPlayer\":true"))
                {
                    return;
                }
                string swapId = swap[1].Split("\"id\":")[1].Split(',')[0];

                LCU.clientRequest("POST", "lol-champ-select/v1/session/swaps/" + swapId + "/accept");
                LCU.clientRequest("POST", "lol-champ-select/v1/ongoing-swap/" + swapId + "/clear");
            }
        }
    }
}
