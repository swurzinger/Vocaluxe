using System;
using System.Collections.Generic;
using System.Text;

using Vocaluxe.Menu;

namespace Vocaluxe.PartyModes
{
    #region Communication
    #region ToScreen
    public struct DataToScreenConfig
    {
        public int NumPlayer;
        public int NumPlayerAtOnce;
        public int NumRounds;
    }

    public struct DataToScreenNames
    {
        public int NumPlayer;
        public List<int> ProfileIDs;
    }

    public struct DataToScreenMain
    {
        public int CurrentRoundNr;
        public int NumPlayerAtOnce;
        public List<Combination> Combs;
        public List<ResultTableRow> ResultTable;
    }

    public class ResultTableRow : IComparable
    {
        public int PlayerID;
        public int NumPlayed;
        public int NumWon;
        public int NumDrawn;
        public int NumLost;
        public int SumSingPoints;
        public int NumGamePoints;

        public int CompareTo(object obj)
        {
            if (obj is ResultTableRow)
            {
                ResultTableRow row = (ResultTableRow)obj;

                int res = row.NumGamePoints.CompareTo(NumGamePoints);
                if (res == 0)
                {
                    res = row.SumSingPoints.CompareTo(SumSingPoints);
                    if (res == 0)
                    {
                        res = row.NumWon.CompareTo(NumWon);
                    }
                }
                return res;
            }

            throw new ArgumentException("object is not a ResultTableRow");
        }
    }

    #endregion ToScreen

    #region FromScreen
    public struct DataFromScreen
    {
        public FromScreenConfig ScreenConfig;
        public FromScreenNames ScreenNames;
        public FromScreenMain ScreenMain;
    }

    public struct FromScreenConfig
    {
        public int NumPlayer;
        public int NumPlayerAtOnce;
        public int NumRounds;
    }

    public struct FromScreenNames
    {
        public bool FadeToConfig;
        public bool FadeToMain;
        public List<int> ProfileIDs;
    }

    public struct FromScreenMain
    {
        public bool FadeToSongSelection;
    }
    #endregion FromScreen
    #endregion Communication

    public sealed class PartyModeChallenge : CPartyMode
    {
        private const int MaxPlayer = 12;
        private const int MinPlayer = 1;
        private const int MaxTeams = 0;
        private const int MinTeams = 0;
        private const int MaxNumRounds = 100;

        enum EStage
        {
            NotStarted,
            Config,
            Names,
            Main,
            Singing
        }

        struct Data
        {
            public int NumPlayer;
            public int NumPlayerAtOnce;
            public int NumRounds;
            public List<int> ProfileIDs;

            public ChallengeRounds Rounds;
            public List<ResultTableRow> ResultTable;

            public int CurrentRoundNr;
        }

        struct Stats
        {
            public int ProfileID;
            public int SingPoints;
            public int GamePoints;
            public int Won;
            public int Drawn;
            public int Lost;
        }

        private DataToScreenConfig ToScreenConfig;
        private DataToScreenNames ToScreenNames;
        private DataToScreenMain ToScreenMain;

        private Data GameData;
        private EStage _Stage;

        public PartyModeChallenge()
        {
            _ScreenSongOptions.Selection.RandomOnly = false;
            _ScreenSongOptions.Selection.PartyMode = true;
            _ScreenSongOptions.Selection.CategoryChangeAllowed = true;
            _ScreenSongOptions.Selection.NumJokers = new int[] { 5, 5 };
            _ScreenSongOptions.Selection.TeamNames = new string[] { "foo", "bar" };

            _ScreenSongOptions.Sorting.SearchString = String.Empty;
            _ScreenSongOptions.Sorting.SearchStringVisible = false;
            _ScreenSongOptions.Sorting.ShowDuetSongs = false;
            
            _Stage = EStage.NotStarted;

            ToScreenConfig = new DataToScreenConfig();
            ToScreenNames = new DataToScreenNames();
            ToScreenMain = new DataToScreenMain();

            GameData = new Data();
            GameData.NumPlayer = 4;
            GameData.NumPlayerAtOnce = 2;
            GameData.NumRounds = 2;
            GameData.CurrentRoundNr = 1;
            GameData.ProfileIDs = new List<int>();        
        }

        public override bool Init()
        {
            _Stage = EStage.NotStarted;

            _ScreenSongOptions.Sorting.IgnoreArticles = _Base.Config.GetIgnoreArticles();
            _ScreenSongOptions.Sorting.SongSorting = ESongSorting.TR_CONFIG_FOLDER;
            _ScreenSongOptions.Sorting.Tabs = EOffOn.TR_CONFIG_ON;

            ToScreenMain.ResultTable = new List<ResultTableRow>();
            GameData.ResultTable = new List<ResultTableRow>();

            return true;
        }

        public override void DataFromScreen(string ScreenName, Object Data)
        {
            DataFromScreen data = new DataFromScreen();
            switch (ScreenName)
            {
                case "PartyScreenChallengeConfig":
                    
                    try
                    {
                        data = (DataFromScreen)Data;
                        GameData.NumPlayer = data.ScreenConfig.NumPlayer;
                        GameData.NumPlayerAtOnce = data.ScreenConfig.NumPlayerAtOnce;
                        GameData.NumRounds = data.ScreenConfig.NumRounds;

                        _Stage = EStage.Config;
                        _Base.Graphics.FadeTo(EScreens.ScreenPartyDummy);
                    }
                    catch (Exception e)
                    {
                        _Base.Log.LogError("Error in party mode challenge. Can't cast received data from screen " + ScreenName + ". " + e.Message);
                    }
                    break;

                case "PartyScreenChallengeNames":
                    try
                    {
                        data = (DataFromScreen)Data;
                        if (data.ScreenNames.FadeToConfig)
                            _Stage = EStage.NotStarted;
                        else
                        {
                            GameData.ProfileIDs = data.ScreenNames.ProfileIDs;
                            _Stage = EStage.Names;
                        }

                        _Base.Graphics.FadeTo(EScreens.ScreenPartyDummy);
                    }
                    catch (Exception e)
                    {
                        _Base.Log.LogError("Error in party mode challenge. Can't cast received data from screen " + ScreenName + ". " + e.Message);
                    }
                    break;

                case "PartyScreenChallengeMain":
                    try
                    {
                        data = (DataFromScreen)Data;
                        if (data.ScreenMain.FadeToSongSelection)
                            _Stage = EStage.Singing;
                    }
                    catch (Exception e)
                    {
                        _Base.Log.LogError("Error in party mode challenge. Can't cast received data from screen " + ScreenName + ". " + e.Message);
                    }


                    if (_Stage == EStage.Singing)
                        StartNextRound();
                    break;

                default:
                    _Base.Log.LogError("Error in party mode challenge. Wrong screen is sending: " + ScreenName);
                    break;
            }
        }

        public override void UpdateGame()
        {
            if (_Base.Songs.GetCurrentCategoryIndex() != -1)
                _ScreenSongOptions.Selection.RandomOnly = true;
            else
                _ScreenSongOptions.Selection.RandomOnly = false;
        }

        public override CMenuParty GetNextPartyScreen(out EScreens AlternativeScreen)
        {
            CMenuParty Screen = null;
            AlternativeScreen = EScreens.ScreenSong;

            switch (_Stage)
            {
                case EStage.NotStarted:
                    _Screens.TryGetValue("PartyScreenChallengeConfig", out Screen);
                    if (_Screens != null)
                    {
                        ToScreenConfig.NumPlayer = GameData.NumPlayer;
                        ToScreenConfig.NumPlayerAtOnce = GameData.NumPlayerAtOnce;
                        ToScreenConfig.NumRounds = GameData.NumRounds;
                        Screen.DataToScreen(ToScreenConfig);
                    }
                    break;
                case EStage.Config:
                    _Screens.TryGetValue("PartyScreenChallengeNames", out Screen);
                    if (_Screens != null)
                    {
                        ToScreenNames.NumPlayer = GameData.NumPlayer;
                        ToScreenNames.ProfileIDs = GameData.ProfileIDs;
                        Screen.DataToScreen(ToScreenNames);
                    }
                    break;
                case EStage.Names:
                    _Screens.TryGetValue("PartyScreenChallengeMain", out Screen);
                    if (_Screens != null)
                    {
                        _Base.Songs.ResetPartySongSung();
                        GameData.Rounds = new ChallengeRounds(GameData.NumRounds, GameData.NumPlayer, GameData.NumPlayerAtOnce);
                        GameData.CurrentRoundNr = 1;
                        ToScreenMain.CurrentRoundNr = 1;
                        ToScreenMain.NumPlayerAtOnce = GameData.NumPlayerAtOnce;
                        ToScreenMain.Combs = GameData.Rounds.Rounds;
                        UpdateScores();
                        ToScreenMain.ResultTable = GameData.ResultTable;
                        Screen.DataToScreen(ToScreenMain);
                    }
                    break;
                case EStage.Main:
                    //nothing to do
                    break;
                case EStage.Singing:
                    _Screens.TryGetValue("PartyScreenChallengeMain", out Screen);
                    if (_Screens != null)
                    {
                        UpdateScores();
                        ToScreenMain.CurrentRoundNr = GameData.CurrentRoundNr;
                        ToScreenMain.Combs = GameData.Rounds.Rounds;

                        Screen.DataToScreen(ToScreenMain);
                    }
                    break;
                default:
                    break;
            }
            
            return Screen;
        }

        public override EScreens GetStartScreen()
        {
            return EScreens.ScreenPartyDummy;
        }

        public override EScreens GetMainScreen()
        {
            return EScreens.ScreenPartyDummy;
        }

        public override ScreenSongOptions GetScreenSongOptions()
        {
            _ScreenSongOptions.Sorting.SongSorting = _Base.Config.GetSongSorting();
            _ScreenSongOptions.Sorting.Tabs = _Base.Config.GetTabs();
            _ScreenSongOptions.Sorting.IgnoreArticles = _Base.Config.GetIgnoreArticles();

            return _ScreenSongOptions;
        }

        public override void SetSearchString(string SearchString, bool Visible)
        {
            _ScreenSongOptions.Sorting.SearchString = SearchString;
            _ScreenSongOptions.Sorting.SearchStringVisible = Visible;
        }

        public override int GetMaxPlayer()
        {
            return MaxPlayer;
        }

        public override int GetMinPlayer()
        {
            return MinPlayer;
        }

        public override int GetMaxTeams()
        {
            return MaxTeams;
        }

        public override int GetMinTeams()
        {
            return MinTeams;
        }

        public override int GetMaxNumRounds()
        {
            return MaxNumRounds;
        }

        public override void JokerUsed(int TeamNr)
        {
            if (_ScreenSongOptions.Selection.NumJokers == null)
                return;

            if (TeamNr >= _ScreenSongOptions.Selection.NumJokers.Length)
                return;

            if (!_ScreenSongOptions.Selection.CategoryChangeAllowed)
                _ScreenSongOptions.Selection.NumJokers[TeamNr]--;

            _ScreenSongOptions.Selection.RandomOnly = true;
            _ScreenSongOptions.Selection.CategoryChangeAllowed = false;
        }

        public override void SongSelected(int SongID)
        {

            EGameMode gm = EGameMode.TR_GAMEMODE_NORMAL;

            _Base.Game.Reset();
            _Base.Game.ClearSongs();
            _Base.Game.AddSong(SongID, gm);
            _Base.Game.SetNumPlayer(GameData.NumPlayerAtOnce);

            SPlayer[] player = _Base.Game.GetPlayer();
            if (player == null)
                return;

            if (player.Length < GameData.NumPlayerAtOnce)
                return;

            SProfile[] profiles = _Base.Profiles.GetProfiles();
            Combination c = GameData.Rounds.GetRound(GameData.CurrentRoundNr - 1);

            for (int i = 0; i < GameData.NumPlayerAtOnce; i++)
            {
                //default values
                player[i].Name = "foobar";
                player[i].Difficulty = EGameDifficulty.TR_CONFIG_EASY;
                player[i].ProfileID = -1;

                //try to fill with the right data
                if (c != null)
                {
                    if (GameData.ProfileIDs[c.Player[i]] < profiles.Length)
                    {
                        player[i].Name = profiles[GameData.ProfileIDs[c.Player[i]]].PlayerName;
                        player[i].Difficulty = profiles[GameData.ProfileIDs[c.Player[i]]].Difficulty;
                        player[i].ProfileID = GameData.ProfileIDs[c.Player[i]];
                    }
                }
            }

            _Base.Songs.AddPartySongSung(SongID);
            _Base.Graphics.FadeTo(EScreens.ScreenSing);
        }

        public override void LeavingHighscore()
        {
            //TODO:
            _Base.Graphics.FadeTo(EScreens.ScreenPartyDummy);
        }

        private void StartNextRound()
        {
            _ScreenSongOptions.Selection.RandomOnly = false;
            _ScreenSongOptions.Selection.CategoryChangeAllowed = true;
            GameData.CurrentRoundNr++;
            SetNumJokers();
            SetTeamNames();
            _Base.Graphics.FadeTo(EScreens.ScreenSong);
        }

        private void SetNumJokers()
        {
            switch (GameData.NumPlayerAtOnce)
            {
                case 1:
                    _ScreenSongOptions.Selection.NumJokers = new int[] { 10 };
                    break;

                case 2:
                    _ScreenSongOptions.Selection.NumJokers = new int[] { 5, 5 };
                    break;

                case 3:
                    _ScreenSongOptions.Selection.NumJokers = new int[] { 4, 4, 4 };
                    break;

                case 4:
                    _ScreenSongOptions.Selection.NumJokers = new int[] { 3, 3, 3, 3 };
                    break;

                case 5:
                    _ScreenSongOptions.Selection.NumJokers = new int[] { 2, 2, 2, 2, 2 };
                    break;

                case 6:
                    _ScreenSongOptions.Selection.NumJokers = new int[] { 2, 2, 2, 2, 2, 2 };
                    break;
                default:
                    _ScreenSongOptions.Selection.NumJokers = new int[] { 5, 5 };
                    break;
            }
        }

        private void SetTeamNames()
        {
            SProfile[] profiles = _Base.Profiles.GetProfiles();

            if (profiles == null)
            {
                _ScreenSongOptions.Selection.TeamNames = new string[] { "foo", "bar" };
                return;
            }

            if (GameData.NumPlayerAtOnce < 1 || GameData.ProfileIDs.Count < GameData.NumPlayerAtOnce || profiles.Length < GameData.NumPlayerAtOnce)
            {
                _ScreenSongOptions.Selection.TeamNames = new string[] { "foo", "bar" };
                return;
            }
            
            _ScreenSongOptions.Selection.TeamNames = new string[GameData.NumPlayerAtOnce];
            Combination c = GameData.Rounds.GetRound(GameData.CurrentRoundNr - 1);

            for (int i = 0; i < GameData.NumPlayerAtOnce; i++)
            {
                if (c != null)
                {
                    if (GameData.ProfileIDs[c.Player[i]] < profiles.Length)
                        _ScreenSongOptions.Selection.TeamNames[i] = profiles[GameData.ProfileIDs[c.Player[i]]].PlayerName;
                    else
                        _ScreenSongOptions.Selection.TeamNames[i] = "foobar";
                }
                else
                    _ScreenSongOptions.Selection.TeamNames[i] = "foobar";
            }
        }

        private void UpdateScores()
        {
            if (GameData.ResultTable.Count == 0)
            {
                for (int i = 0; i < GameData.NumPlayer; i++)
                {
                    ResultTableRow row = new ResultTableRow();
                    row.PlayerID = GameData.ProfileIDs[i];
                    row.NumPlayed = 0;
                    row.NumWon = 0;
                    row.NumDrawn = 0;
                    row.NumLost = 0;
                    row.SumSingPoints = 0;
                    row.NumGamePoints = 0;
                    GameData.ResultTable.Add(row);
                }
            }
            else
            {
                SPlayer[] results = _Base.Game.GetPlayer();
                if (results == null)
                    return;

                if (results.Length < GameData.NumPlayerAtOnce)
                    return;

                List<Stats> points = GetPointsForPlayer(results);

                for (int i = 0; i < GameData.NumPlayerAtOnce; i++)
                {
                    int index = -1;
                    for (int j = 0; j < GameData.ResultTable.Count; j++)
			        {
                        if (points[i].ProfileID == GameData.ResultTable[j].PlayerID)
                        {
                            index = j;
                            break;
                        }
			        }

                    if (index != -1)
                    {
                        ResultTableRow row = GameData.ResultTable[index];

                        row.NumPlayed++;
                        row.NumWon += points[i].Won;
                        row.NumDrawn += points[i].Drawn;
                        row.NumLost += points[i].Lost;
                        row.SumSingPoints += points[i].SingPoints;
                        row.NumGamePoints += points[i].GamePoints;

                        GameData.ResultTable[index] = row;
                    }
                }
            }

            GameData.ResultTable.Sort();
        }

        private List<Stats> GetPointsForPlayer(SPlayer[] Results)
        {
            List<Stats> result = new List<Stats>();
            for(int i = 0; i < GameData.NumPlayerAtOnce; i++)
            {
                Stats stat = new Stats();
                stat.ProfileID = Results[i].ProfileID;
                stat.SingPoints = (int)Results[i].Points;
                stat.Won = 0;
                stat.Drawn = 0;
                stat.Lost = 0;
                stat.GamePoints = 0;
                result.Add(stat);
            }

            result.Sort(delegate(Stats s1, Stats s2) { return s1.SingPoints.CompareTo(s2.SingPoints); });

            int current = result[result.Count - 1].SingPoints;
            int points = result.Count;

            for (int i = result.Count - 1; i >= 0; i--)
            {
                Stats res = result[i];

                if (i < result.Count - 1)
                {
                    if (current > res.SingPoints)
                    {
                        res.GamePoints = i * 2;
                    }
                    else
                        res.GamePoints = points;
                }
                else
                    res.GamePoints = i * 2;

                current = res.SingPoints;
                points = res.GamePoints;

                result[i] = res;
            }

            return result;
        }
    }
}