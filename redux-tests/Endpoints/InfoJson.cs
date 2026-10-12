using System.Text.RegularExpressions;

namespace redux_tests;
#pragma warning disable CS1591

// /info names each visualization's format=frames picture type in pictureType (#841). The picture tests guard that nothing
// else of the frames plumbing reaches /info; they check the body without that one field.
public static partial class InfoJson {
    [GeneratedRegex(@"""pictureType"":\s*(null|""[A-Za-z]+""),?")]
    private static partial Regex PictureTypeField();

    /// <summary>The /info body without its pictureType fields.</summary>
    public static string WithoutPictureType(string body) => PictureTypeField().Replace(body, "");
}
