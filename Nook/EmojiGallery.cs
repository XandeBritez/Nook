namespace Nook;

/// <summary>Galeria curada de emojis para o campo Ícone do editor.
/// O ComboBox é editável, então qualquer emoji fora da lista também vale.</summary>
internal static class EmojiGallery
{
    public static IReadOnlyList<string> All { get; } = new[]
    {
        // Usados nos defaults
        "🌐", "🧩", "⌨️", "🔒", "🔇", "🔊", "🔉", "📸", "📋", "🌙",
        "🗑️", "💤", "🚀", "⚡", "📌", "⚙️", "•",
        // Apps e web
        "🌍", "🔍", "📧", "💬", "📞", "📹", "📺", "🎮", "🎲", "🎯",
        "🎨", "🖼️", "📝", "📚", "📖", "🔖", "📎", "📁", "📂", "💾",
        // Dev e tech
        "💻", "🖥️", "🖱️", "💡", "🐛", "🧪", "📊", "📈", "🔧", "🛠️",
        "🔑", "🔓", "🤖", "👾", "📡", "🛰️", "💠", "🔷", "🟢", "🔴", "🟡",
        // Mídia
        "🎵", "🎧", "🎤", "🎬", "🍿",
        // Sistema e energia
        "🔋", "🔌", "🧹", "♻️", "⏻", "⏸️", "▶️", "⏹️", "🔁", "☀️", "⭐", "✨",
        // Dia a dia
        "🏠", "💰", "📅", "⏰", "🧭", "🗺️", "🔗", "✂️", "🏷️", "❤️",
        "✅", "❌", "⚠️", "🚫", "🆕", "🔝",
    };
}
