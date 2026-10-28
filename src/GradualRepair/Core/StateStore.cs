using System;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace GradualRepair
{
    // A progress record is bound to the exact bytes written by a successful native save.
    // Unsaved purchases remain only in memory. A vanilla reload therefore rolls both back.
    public sealed class StateStore
    {
        readonly string root, recoveryRoot;
        public bool RecoveredLastLoad { get; private set; }
        public StateStore(string root, string recoveryRoot = null) { this.root = root; this.recoveryRoot = recoveryRoot; Directory.CreateDirectory(root); }
        public static string HashFile(string path)
        { using (var sha = SHA256.Create()) using (var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", ""); }
        string PathFor(string hash)
        { if (hash == null || hash.Length != 64 || System.Linq.Enumerable.Any(hash, c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid hash"); return Path.Combine(root, hash + ".json"); }
        public RepairState Load(string savePath)
        {
            RecoveredLastLoad = false;
            string hash = HashFile(savePath), record = PathFor(hash);
            string recovery = recoveryRoot == null ? null : Path.Combine(recoveryRoot, hash + ".json");
            RepairState primary = null, backup = null; Exception readError = null;
            if (File.Exists(record)) try { primary = ReadState(record, hash); } catch (Exception ex) { readError = ex; }
            if (recovery != null && File.Exists(recovery)) try { backup = ReadState(recovery, hash); } catch (Exception ex) { if (readError == null) readError = ex; }
            if (backup != null && (primary == null || backup.LedgerRevision > primary.LedgerRevision)) { RecoveredLastLoad = true; return backup; }
            if (primary != null) return primary;
            if (readError != null) throw new InvalidDataException("No valid matching repair ledger or recovery copy", readError);
            return new RepairState { SaveHash = hash, ExteriorProject = false };
        }
        static RepairState ReadState(string record, string hash)
        {
            var state = JsonConvert.DeserializeObject<RepairState>(File.ReadAllText(record));
            RepairModel.UpgradeWallLayout(state);
            RepairModel.Validate(state);
            if (state.SaveHash != hash) throw new InvalidDataException("Save/progress hash mismatch");
            state.LegacyFormat = state.Schema == 1;
            return state;
        }
        static void WriteRecord(string path, string json)
        { string temporary = path + ".tmp"; File.WriteAllText(temporary, json); if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path); }
        public void Commit(string savePath, RepairState current)
        {
            RepairModel.Validate(current);
            var copy = current.Copy(); copy.SaveHash = HashFile(savePath); copy.LedgerRevision = Math.Max(current.LedgerRevision + 1, DateTime.UtcNow.Ticks);
            string record = PathFor(copy.SaveHash);
            string json = JsonConvert.SerializeObject(copy, Formatting.Indented);
            // A separate mod-only location survives a failure of the primary ledger.
            // Recovery is used only for exactly matching native save bytes.
            Exception recoveryError = null;
            if (recoveryRoot != null) try { Directory.CreateDirectory(recoveryRoot); WriteRecord(Path.Combine(recoveryRoot, copy.SaveHash + ".json"), json); } catch (Exception ex) { recoveryError = ex; }
            try { WriteRecord(record, json); } catch (Exception primary) { throw new IOException("Primary repair ledger failed. " + (recoveryRoot != null && recoveryError == null ? "Matching recovery copy is available." : "Recovery copy unavailable; keep this session open and retry."), primary); }
            current.SaveHash = copy.SaveHash; current.LedgerRevision = copy.LedgerRevision;
        }
    }
}
