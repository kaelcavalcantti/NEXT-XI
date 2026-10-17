using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core.Persistence;

namespace NextXI.Core.Career
{
    [Serializable]
    public sealed class CareerSlotInfo
    {
        public string Id;
        public string SaveName;
        public string AthleteName;
        public string ClubName;
        public int Season;
        public int Day;
        public int Overall;
        public string LastSavedUtc;
        public bool Corrupted;
        public string Error;
    }

    /// <summary>Multiple independent career saves (one file each) with versioning and recovery.</summary>
    public sealed class CareerStore
    {
        private const string Prefix = "career_";
        private const string Kind = "career";
        private readonly SaveSystem _saves;

        public CareerStore(SaveSystem saves) { _saves = saves; }

        public static string FileName(string id) => Prefix + id + ".json";

        public void Save(CareerState s)
        {
            s.LastSavedUtc = DateTime.UtcNow.ToString("o");
            _saves.Save(FileName(s.Id), Kind, CareerState.FormatVersion, s);
        }

        public LoadStatus TryLoad(string id, out CareerState state, out string error)
        {
            var st = _saves.TryLoad(FileName(id), Kind, CareerState.FormatVersion, out state, out error);
            if ((st == LoadStatus.Ok || st == LoadStatus.RecoveredFromBackup) && (state.World == null || state.Athlete == null))
            {
                error = "save is missing world or athlete";
                state = null;
                return LoadStatus.Corrupted;
            }
            return st;
        }

        public void Delete(string id) => _saves.Delete(FileName(id));

        public List<CareerSlotInfo> List()
        {
            var list = new List<CareerSlotInfo>();
            foreach (var file in _saves.List(Prefix).OrderBy(f => f))
            {
                string id = file.Substring(Prefix.Length, file.Length - Prefix.Length - ".json".Length);
                var status = TryLoad(id, out var s, out var err);
                if (s != null)
                    list.Add(new CareerSlotInfo
                    {
                        Id = s.Id, SaveName = s.SaveName, AthleteName = s.Athlete.DisplayName, ClubName = s.Club?.Name ?? "Sem clube",
                        Season = s.World.Season, Day = s.Day, Overall = s.Athlete.Overall, LastSavedUtc = s.LastSavedUtc
                    });
                else list.Add(new CareerSlotInfo { Id = id, SaveName = id, Corrupted = true, Error = err });
            }
            return list.OrderByDescending(x => x.LastSavedUtc).ToList();
        }
    }
}
