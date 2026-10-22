using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace UmaDesktop
{
    // Only mini body clips enter the scheduler. Facial/ear/position/camera clips
    // are separate tracks, never selected as the main action.
    public sealed class MiniMotionCatalog
    {
        public sealed class Costume
        {
            public string Id, Name;
            public int Category; // 0 race, 1 school/training, 2 other
        }

        public sealed class Character
        {
            public CharaEntry Entry;
            public List<UmaDatabaseEntry> Specials = new List<UmaDatabaseEntry>();
            public List<Costume> Costumes = new List<Costume>();
        }

        public readonly List<Character> Characters = new List<Character>();
        public readonly List<UmaDatabaseEntry> Walks = new List<UmaDatabaseEntry>();
        public readonly List<UmaDatabaseEntry> Runs = new List<UmaDatabaseEntry>();
        private readonly Dictionary<string, UmaDatabaseEntry> entries;
        private readonly Dictionary<string, bool> availability = new Dictionary<string, bool>();

        public MiniMotionCatalog(DesktopSettings settings)
        {
            entries = UmaViewerMain.Instance.AbList;
            foreach (string stem in new[] { "walk01_loop", "walk02_loop" }) AddLocomotion(Walks, stem);
            foreach (string stem in new[] { "run00_loop", "run02_loop", "run03_loop" }) AddLocomotion(Runs, stem);
            if (Walks.Count == 0 || Runs.Count == 0)
                throw new InvalidDataException("Mini walk/run clips or their dependencies are missing.");

            var dresses = UmaDatabaseController.Instance.DressData;
            var bodyEntries = entries.Values.Where(e => e.Name.StartsWith("3d/chara/mini/body/", StringComparison.Ordinal)).ToArray();
            var common = new List<Costume>();
            foreach (var model in bodyEntries.Where(e => e.Name.Contains("/pfb_mbdy") && e.Name.EndsWith("_0")))
            {
                string[] parts = model.Name.Substring(model.Name.LastIndexOf('/') + 1).Split('_');
                if (parts.Length != 5 || !int.TryParse(parts[1].Substring(4), out int bodyType) || bodyType >= 100) continue;
                int sub = int.Parse(parts[2]);
                DataRow dress = dresses.FirstOrDefault(r => Convert.ToInt32(r["chara_id"]) == 0 &&
                    Convert.ToInt32(r["body_type"]) == bodyType && Convert.ToInt32(r["body_type_sub"]) == sub &&
                    Convert.ToInt32(r["have_mini"]) == 1);
                if (dress == null || !Available(model.Name)) continue;
                string id = parts[1].Substring(4) + "_" + parts[2] + "_" + parts[3];
                if (!common.Any(c => c.Id == id)) common.Add(new Costume { Id = id,
                    Name = dress["dressname"].ToString(), Category = bodyType == 2 || bodyType == 3 || bodyType == 16 ? 1 : 2 });
            }
            foreach (DataRow row in UmaDatabaseController.Instance.CharaData)
            {
                int id = Convert.ToInt32(row["id"]);
                string hair = $"3d/chara/mini/head/mchr{id}_00/pfb_mchr{id}_00_hair";
                string face = "3d/chara/mini/head/mchr0001_00/pfb_mchr0001_00_face0";
                if (!Available(hair) || !Available(face)) continue;
                var character = new Character { Entry = new CharaEntry { Id = id, Name = row["charaname"].ToString() } };
                foreach (DataRow dress in dresses.Where(r => Convert.ToInt32(r["chara_id"]) == id && Convert.ToInt32(r["have_mini"]) == 1))
                {
                    string costume = Convert.ToInt32(dress["body_type_sub"]).ToString("00");
                    string body = $"3d/chara/mini/body/mbdy{id}_{costume}/pfb_mbdy{id}_{costume}";
                    if (!Available(body) || character.Costumes.Any(c => c.Id == costume)) continue;
                    character.Costumes.Add(new Costume { Id = costume, Name = dress["dressname"].ToString(),
                        Category = Convert.ToInt32(dress["use_race"]) == 1 ? 0 : 2 });
                }
                // Require a personal mini body: common uniforms alone must not admit NPC rows.
                if (character.Costumes.Count == 0) continue;
                string skin = row["skin"].ToString();
                foreach (Costume costume in common)
                {
                    string shortId = costume.Id.Substring(0, costume.Id.LastIndexOf('_'));
                    string pattern = costume.Id.StartsWith("0003_") ? $"tex_mbdy{shortId}_00_{skin}_0" : $"tex_mbdy{costume.Id}_{skin}_0";
                    if (bodyEntries.Any(e => e.Name.Contains(pattern) && Available(e.Name))) character.Costumes.Add(costume);
                }
                string prefix = $"3d/motion/mini/event/body/chara/chr{id}_00/";
                foreach (UmaDatabaseEntry motion in entries.Values)
                {
                    if (!motion.Name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (motion.Name.EndsWith("_s", StringComparison.Ordinal) || motion.Name.EndsWith("_e", StringComparison.Ordinal)) continue;
                    if (Available(motion.Name)) character.Specials.Add(motion);
                }
                Characters.Add(character);
            }
            Characters.Sort((a, b) => a.Entry.Id.CompareTo(b.Entry.Id));
            if (Characters.Count == 0) throw new InvalidDataException("No complete mini character costume was found.");
        }

        public Costume PickCostume(Character character, Random random, int forcedCategory = -1, string excludeId = null, IEnumerable<Costume> candidates = null)
        {
            var choices=(candidates??character.Costumes).ToList();
            var available = choices.Where(c => c.Id != excludeId).ToList();
            if (available.Count == 0) available = choices;
            if (available.Count == 0) return null;
            int category = forcedCategory >= 0 && available.Any(c => c.Category == forcedCategory) ? forcedCategory :
                DesktopChoice.CostumeCategory(random, available.Any(c => c.Category == 0),
                    available.Any(c => c.Category == 1), available.Any(c => c.Category == 2));
            var pool = available.Where(c => c.Category == category).ToArray();
            return pool[random.Next(pool.Length)];
        }

        public List<UmaDatabaseEntry> CostumeSpecials(Character character, Costume costume)
        {
            string personal = costume.Id.Length < 4 ? costume.Id : character.Costumes.First(c => c.Id.Length < 4).Id;
            var matching = character.Specials.Where(e => e.Name.Contains($"_chr{character.Entry.Id}_{personal}_")).ToList();
            if (matching.Count == 0) matching = character.Specials.Where(e => e.Name.Contains($"_chr{character.Entry.Id}_00_")).ToList();
            var expressive = matching.Where(e => !e.Name.Contains("_idle")).ToList();
            return expressive.Count > 0 ? expressive : matching;
        }

        public UmaDatabaseEntry Find(string name) { return Available(name) ? entries[name] : null; }

        private void AddLocomotion(List<UmaDatabaseEntry> list, string stem)
        {
            string name = "3d/motion/mini/event/body/type00/anm_min_eve_type00_" + stem;
            if (Available(name)) list.Add(entries[name]);
        }

        public UmaDatabaseEntry Companion(UmaDatabaseEntry entry, string suffix)
        {
            string stem = entry.Name.EndsWith("_loop", StringComparison.Ordinal)
                ? entry.Name.Substring(0, entry.Name.Length - 5) : entry.Name;
            string name = stem + suffix;
            return Available(name) ? entries[name] : null;
        }

        private bool Available(string name) { return Available(name, new HashSet<string>()); }

        private bool Available(string name, HashSet<string> visiting)
        {
            if (availability.TryGetValue(name, out bool cached)) return cached;
            if (!entries.TryGetValue(name, out UmaDatabaseEntry entry) || !File.Exists(entry.Path))
                return availability[name] = false;
            if (!visiting.Add(name)) return true;
            foreach (string dependency in (entry.Prerequisites ?? "").Split(';'))
            {
                string dependencyName = dependency.Trim();
                if (dependencyName.Length > 0 && !Available(dependencyName, visiting))
                {
                    visiting.Remove(name);
                    return availability[name] = false;
                }
            }
            visiting.Remove(name);
            return availability[name] = true;
        }
    }
}
