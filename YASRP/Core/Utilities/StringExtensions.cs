namespace YASRP.Core.Utilities;

public static class StringExtensions {
    // 基本方法：将字符串以逗号为分隔符转换为 IEnumerable<string>
    public static IEnumerable<string> SplitByComma(this string input) {
        if (string.IsNullOrEmpty(input))
            return Enumerable.Empty<string>();

        return input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim());
    }

    // 转换为 List<string>
    public static List<string> SplitByCommaToList(this string input) {
        return input.SplitByComma().ToList();
    }

    // 转换为数组
    public static string[] SplitByCommaToArray(this string input) {
        return input.SplitByComma().ToArray();
    }

    // 转换为 HashSet<string>
    public static HashSet<string> SplitByCommaToHashSet(this string input) {
        return new HashSet<string>(input.SplitByComma());
    }

    // 转换为只读集合
    public static IReadOnlyCollection<string> SplitByCommaToReadOnlyCollection(this string input) {
        return input.SplitByComma().ToList().AsReadOnly();
    }

    // 转换为 IEnumerable<string>，并过滤掉空字符串
    public static IEnumerable<string> SplitByCommaAndFilterEmpty(this string input) {
        return input.SplitByComma().Where(s => !string.IsNullOrEmpty(s));
    }

    // 转换为 IEnumerable<string>，并去重
    public static IEnumerable<string> SplitByCommaAndDistinct(this string input) {
        return input.SplitByComma().Distinct();
    }
}