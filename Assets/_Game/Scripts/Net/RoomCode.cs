using System.Text;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Net
{
    /// <summary>Six-letter room codes for Steam lobbies, shown "ABC-DEF".</summary>
    public static class RoomCode
    {
        // No I/O/0/1 look-alikes.
        private const string ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ";

        public static string New()
        {
            StringBuilder lCode = new StringBuilder(6);
            System.Random lRandom = new System.Random();
            for (int i = 0; i < 6; i++) lCode.Append(ALPHABET[lRandom.Next(ALPHABET.Length)]);
            return Format(lCode.ToString());
        }

        public static string Clean(string pCode)
        {
            if (string.IsNullOrEmpty(pCode)) return "";
            StringBuilder lClean = new StringBuilder(6);
            foreach (char lChar in pCode.ToUpperInvariant()) if (char.IsLetter(lChar)) lClean.Append(lChar);
            return lClean.ToString();
        }

        public static string Format(string pCode)
        {
            string lClean = Clean(pCode);
            return lClean.Length == 6 ? lClean.Substring(0, 3) + "-" + lClean.Substring(3) : lClean;
        }
    }
}
