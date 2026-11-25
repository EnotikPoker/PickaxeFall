using System;

namespace PickaxeFall
{
    public static class MineralCatalog
    {
        public static bool Found(PlayerProfile profile, BlockKind kind) => profile.discovered[(int)kind] ||
            profile.resources[(int)kind] > 0 || profile.lifetimeMined[(int)kind] > 0;
        public static bool InMap(BlockKind kind, MapId map)
        {
            if (!ResourceInventory.IsResource(kind)) return false;
            if (kind == BlockKind.Obsidian) return map != MapId.Nether;
            if (kind == BlockKind.Copper || kind == BlockKind.Gold || kind == BlockKind.Amethyst) return map != MapId.Nether;
            if (kind >= BlockKind.Netherrack) return map == MapId.Nether;
            if (kind >= BlockKind.Sand && kind <= BlockKind.SmokyQuartz) return map == MapId.Desert;
            return map == MapId.Mine;
        }
        public static string Location(BlockKind kind, MapId map) => kind switch
        {
            BlockKind.Obsidian => L.T("Там, где вода встретилась с лавой."),
            BlockKind.Amethyst when map == MapId.Mine => L.T("В редких полых жеодах под землёй."),
            BlockKind.Dirt or BlockKind.Sand => L.T("У поверхности."),
            BlockKind.Stone => L.T("Каменный слой обычной шахты."),
            BlockKind.Deepstone or BlockKind.Emerald or BlockKind.Lapis or BlockKind.Redstone => L.T("Глубинносланцевый слой обычной шахты."),
            BlockKind.Sandstone or BlockKind.Copper or BlockKind.Malachite or BlockKind.Turquoise when map == MapId.Desert => L.T("Песчаниковый слой пустыни."),
            BlockKind.DarkQuartzite or BlockKind.Garnet or BlockKind.SmokyQuartz or BlockKind.Amethyst when map == MapId.Desert => L.T("Тёмный кварцит в глубине пустыни."),
            BlockKind.Gold when map == MapId.Desert => L.T("Оба каменных слоя пустыни; в тёмном кварците чаще."),
            BlockKind.Netherrack or BlockKind.Sulfur => L.T("Верхний слой ада, среди незерака."),
            BlockKind.Quartz => L.T("Незерак и базальт в аду."),
            BlockKind.Basalt or BlockKind.Cinnabar => L.T("Базальтовый слой ада."),
            BlockKind.FireOpal => L.T("Базальт и чернит в аду."),
            BlockKind.Blackstone or BlockKind.AncientDebris or BlockKind.SoulCrystal => L.T("Чернит в глубине ада."),
            _ => L.T("Жилы в обычной шахте; более ценные руды встречаются глубже.")
        };
        public static string Note(BlockKind kind) => kind switch
        {
            BlockKind.SmokyQuartz => L.T("Пустынный минерал. В рецептах не заменяется кварцем из ада."),
            BlockKind.Quartz => L.T("Кварц из ада. Не заменяет дымчатый кварц из пустыни."),
            BlockKind.Redstone => L.T("Загорается от касания и может поджечь соседний динамит."),
            BlockKind.Obsidian => L.T("Очень прочный. Добывается любой киркой и нужен для портала."),
            BlockKind.Amethyst => L.T("Кристаллы дают и оболочка жеоды, и кристаллы внутри неё."),
            BlockKind.AncientDebris or BlockKind.SoulCrystal or BlockKind.Emerald => L.T("Редкая находка. Пригодится для дорогих улучшений."),
            BlockKind.Dirt or BlockKind.Sand => L.T("«Землекоп» иногда даёт ещё один блок при прямой добыче."),
            _ => L.T("Сохраняй для улучшений или продавай в рюкзаке.")
        };
        public static long TotalMined(PlayerProfile profile)
        {
            long total = 0;
            foreach (var kind in ResourceInventory.Materials) total = ProfileJournal.Add(total, profile.lifetimeMined[(int)kind]);
            return total;
        }
        public static int FoundCount(PlayerProfile profile)
        {
            int count = 0;
            foreach (var kind in ResourceInventory.Materials) if (Found(profile, kind)) count++;
            return count;
        }
    }
}
