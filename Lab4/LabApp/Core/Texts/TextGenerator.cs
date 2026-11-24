using System.Text;

namespace LabApp.Core.Texts;

/// <summary>
/// Генератор учебных текстов для экспериментов (задание 3): тексты английскими словами
/// разной длины — 100, 500, 1000, 2000, 5000 слов и более. Слова выбираются из базового
/// словаря с распределением Ципфа (частотность убывает по рангу): у текста есть и очень
/// частые слова, и редкие — как в настоящем языке, поэтому подсчёт вхождений показательен.
/// Генерация детерминирована: фиксированный сид → один и тот же текст (замеры воспроизводимы).
/// </summary>
public static class TextGenerator
{
    /// <summary>Длины текстов по умолчанию — как в задании (100…5000 слов «или более»).</summary>
    public static readonly int[] DefaultLengths = [100, 500, 1000, 2000, 5000];

    public static string FileNameFor(int wordCount) => $"text_{wordCount}.txt";

    /// <summary>Максимальная длина текста, который можно сгенерировать с сайта.</summary>
    public const int MaxGenerateWords = 100_000;

    /// <summary>Генерирует текст из wordCount слов. seed фиксирует «случайность» — текст воспроизводим.</summary>
    public static string Generate(int wordCount, int seed)
    {
        var rnd = new Random(seed);
        var words = new List<string>(wordCount);
        double[] cumulative = BuildCumulativeWeights();
        for (int i = 0; i < wordCount; i++)
            words.Add(Vocabulary[PickRank(rnd, cumulative)]);

        // Расставляем знаки препинания и переносим строки — как в настоящем тексте.
        // При разборе на слова знаки отбрасываются, поэтому на сортировку они не влияют.
        var sb = new StringBuilder(capacity: wordCount * 7);
        int inSentence = 0, sentenceLength = rnd.Next(4, 10), lineWords = 0;
        for (int i = 0; i < words.Count; i++)
        {
            sb.Append(words[i]);
            inSentence++;
            lineWords++;

            bool last = i == words.Count - 1;
            if (last || inSentence >= sentenceLength)
            {
                sb.Append('.');
                inSentence = 0;
                sentenceLength = rnd.Next(4, 10);
                sb.Append('\n');
                lineWords = 0;
            }
            else if (rnd.NextDouble() < 0.12)
            {
                sb.Append(',');
                sb.Append(' ');
                lineWords++;
            }
            else if (lineWords >= 9)
            {
                sb.Append('\n');
                lineWords = 0;
            }
            else
            {
                sb.Append(' ');
            }
        }
        return sb.ToString();
    }

    // ---------------- служебные ----------------

    /// <summary>Накопленные веса распределения Ципфа: ранг r даёт вес 1/r^0.9.</summary>
    private static double[] BuildCumulativeWeights()
    {
        var cumulative = new double[Vocabulary.Length];
        double sum = 0;
        for (int i = 0; i < Vocabulary.Length; i++)
        {
            sum += 1.0 / Math.Pow(i + 1, 0.9);
            cumulative[i] = sum;
        }
        return cumulative;
    }

    private static int PickRank(Random rnd, double[] cumulative)
    {
        double target = rnd.NextDouble() * cumulative[^1];
        // Бинарный поиск по накопленным весам.
        int lo = 0, hi = cumulative.Length - 1;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (cumulative[mid] < target) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>
    /// Базовый словарь: сначала частые служебные слова, дальше — тематические.
    /// Английские слова рекомендованы заданием: меньше словоформ, порядок — как в словаре.
    /// </summary>
    private static readonly string[] Vocabulary =
    [
        // служебные слова — самые частые
        "the", "and", "a", "of", "to", "in", "is", "was", "it", "for", "on", "with", "as", "that",
        "this", "he", "she", "they", "we", "you", "not", "at", "but", "from", "by", "have", "has",
        "had", "are", "were", "be", "do", "did", "can", "will", "would", "could", "my", "your",
        "his", "her", "our", "their", "them", "then", "than", "so", "if", "when", "what", "which",
        "who", "how", "where", "why", "all", "some", "many", "more", "most", "very", "just", "also",
        "only", "about", "after", "before", "between", "under", "over", "again", "always", "never",
        // часто встречающиеся слова
        "time", "day", "year", "people", "man", "woman", "child", "word", "letter", "book", "page",
        "story", "song", "game", "ball", "friend", "family", "mother", "father", "sister", "brother",
        "house", "home", "door", "window", "room", "table", "chair", "water", "sun", "moon", "star",
        "sky", "sea", "river", "lake", "tree", "flower", "grass", "forest", "field", "mountain",
        "road", "street", "city", "school", "lesson", "class", "teacher", "student", "pen", "hand",
        "head", "eye", "heart", "food", "bread", "milk", "apple", "fruit", "bird", "cat", "dog",
        "fish", "horse", "wind", "rain", "snow", "cloud", "fire", "earth", "light", "night",
        // глаголы
        "go", "come", "run", "jump", "walk", "play", "read", "write", "draw", "sing", "dance",
        "eat", "drink", "sleep", "work", "help", "love", "like", "know", "think", "say", "tell",
        "ask", "answer", "look", "see", "hear", "find", "take", "give", "make", "build", "open",
        "close", "start", "stop", "live", "laugh", "smile", "fly", "swim", "ride", "drive", "cook",
        "learn", "teach", "grow", "change", "move", "turn", "hold", "keep", "put", "get", "want",
        "use", "try", "call", "need", "feel", "leave", "stay", "wait", "watch", "listen", "speak",
        // прилагательные и наречия
        "big", "small", "long", "short", "new", "old", "good", "bad", "fast", "slow", "hot", "cold",
        "warm", "soft", "hard", "happy", "sad", "kind", "strong", "young", "clean", "dark", "bright",
        "quiet", "loud", "rich", "poor", "easy", "difficult", "important", "beautiful", "funny",
        // менее частые тематические слова
        "winter", "summer", "spring", "autumn", "morning", "evening", "week", "month", "garden",
        "kitchen", "garden", "village", "country", "world", "music", "picture", "letter", "number",
        "question", "example", "idea", "dream", "life", "friend", "smile", "voice", "speed", "color",
    ];
}

/// <summary>
/// Разбор текста на слова: регистр приводим к нижнему, знаки препинания отбрасываем,
/// дефис сохраняем только внутри слова (mother-in-law остаётся одним словом).
/// </summary>
public static class TextNormalizer
{
    public static List<string> ExtractWords(string text)
    {
        var result = new List<string>();
        foreach (string token in text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string word = CleanWord(token);
            if (word.Length > 0)
                result.Add(word);
        }
        return result;
    }

    /// <summary>Очищает одно «сырое» слово: нижний регистр, только буквы/цифры и внутренний дефис.</summary>
    public static string CleanWord(string token)
    {
        var sb = new StringBuilder(token.Length);
        foreach (char ch in token)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
            else if (ch == '-' && sb.Length > 0)
                sb.Append('-'); // дефис между частями слова; в конце слова обрежем
        }
        while (sb.Length > 0 && sb[^1] == '-')
            sb.Length--;
        return sb.ToString();
    }
}
