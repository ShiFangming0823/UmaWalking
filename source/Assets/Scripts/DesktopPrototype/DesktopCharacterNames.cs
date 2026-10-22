using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UmaDesktop
{
    // Japanese IDs/names are immutable resource identity. This table is display-only.
    // Base spellings: UmaViewer Assets/Scripts/localfy/Localize.cs and LocalizeEn.cs.
    // Missing ID 1149 and the formal name of ID 9001 are completed here.
    public static class DesktopCharacterNames
    {
        private static readonly Dictionary<int,string[]> Names=new Dictionary<int,string[]>
        {
            {1001,new[]{"スペシャルウィーク","特别周","Special Week"}},
            {1002,new[]{"サイレンススズカ","无声铃鹿","Silence Suzuka"}},
            {1003,new[]{"トウカイテイオー","东海帝皇","Tokai Teio"}},
            {1004,new[]{"マルゼンスキー","丸善斯基","Maruzensky"}},
            {1005,new[]{"フジキセキ","富士奇石","Fuji Kiseki"}},
            {1006,new[]{"オグリキャップ","小栗帽","Oguri Cap"}},
            {1007,new[]{"ゴールドシップ","黄金船","Gold Ship"}},
            {1008,new[]{"ウオッカ","伏特加","Vodka"}},
            {1009,new[]{"ダイワスカーレット","大和赤骥","Daiwa Scarlet"}},
            {1010,new[]{"タイキシャトル","大树快车","Taiki Shuttle"}},
            {1011,new[]{"グラスワンダー","草上飞","Grass Wonder"}},
            {1012,new[]{"ヒシアマゾン","菱亚马逊","Hishi Amazon"}},
            {1013,new[]{"メジロマックイーン","目白麦昆","Mejiro McQueen"}},
            {1014,new[]{"エルコンドルパサー","神鹰","El Condor Pasa"}},
            {1015,new[]{"テイエムオペラオー","好歌剧","T.M. Opera O"}},
            {1016,new[]{"ナリタブライアン","成田白仁","Narita Brian"}},
            {1017,new[]{"シンボリルドルフ","鲁道夫象征","Symboli Rudolf"}},
            {1018,new[]{"エアグルーヴ","气槽","Air Groove"}},
            {1019,new[]{"アグネスデジタル","爱丽数码","Agnes Digital"}},
            {1020,new[]{"セイウンスカイ","星云天空","Seiun Sky"}},
            {1021,new[]{"タマモクロス","玉藻十字","Tamamo Cross"}},
            {1022,new[]{"ファインモーション","美妙姿势","Fine Motion"}},
            {1023,new[]{"ビワハヤヒデ","琵琶晨光","Biwa Hayahide"}},
            {1024,new[]{"マヤノトップガン","摩耶重炮","Mayano Top Gun"}},
            {1025,new[]{"マンハッタンカフェ","曼城茶座","Manhattan Cafe"}},
            {1026,new[]{"ミホノブルボン","美浦波旁","Mihono Bourbon"}},
            {1027,new[]{"メジロライアン","目白赖恩","Mejiro Ryan"}},
            {1028,new[]{"ヒシアケボノ","菱曙","Hishi Akebono"}},
            {1029,new[]{"ユキノビジン","雪之美人","Yukino Bijin"}},
            {1030,new[]{"ライスシャワー","米浴","Rice Shower"}},
            {1031,new[]{"アイネスフウジン","艾尼斯风神","Ines Fujin"}},
            {1032,new[]{"アグネスタキオン","爱丽速子","Agnes Tachyon"}},
            {1033,new[]{"アドマイヤベガ","爱慕织姬","Admire Vega"}},
            {1034,new[]{"イナリワン","稻荷一","Inari One"}},
            {1035,new[]{"ウイニングチケット","胜利奖券","Winning Ticket"}},
            {1036,new[]{"エアシャカール","空中神宫","Air Shakur"}},
            {1037,new[]{"エイシンフラッシュ","荣进闪耀","Eishin Flash"}},
            {1038,new[]{"カレンチャン","真机伶","Curren Chan"}},
            {1039,new[]{"カワカミプリンセス","川上公主","Kawakami Princess"}},
            {1040,new[]{"ゴールドシチー","黄金城市","Gold City"}},
            {1041,new[]{"サクラバクシンオー","樱花进王","Sakura Bakushin O"}},
            {1042,new[]{"シーキングザパール","采珠","Seeking the Pearl"}},
            {1043,new[]{"シンコウウインディ","新光风","Shinko Windy"}},
            {1044,new[]{"スイープトウショウ","东商变革","Sweep Tosho"}},
            {1045,new[]{"スーパークリーク","超级溪流","Super Creek"}},
            {1046,new[]{"スマートファルコン","醒目飞鹰","Smart Falcon"}},
            {1047,new[]{"ゼンノロブロイ","荒漠英雄","Zenno Rob Roy"}},
            {1048,new[]{"トーセンジョーダン","东瀛佐敦","Tosen Jordan"}},
            {1049,new[]{"ナカヤマフェスタ","中山庆典","Nakayama Festa"}},
            {1050,new[]{"ナリタタイシン","成田大进","Narita Taishin"}},
            {1051,new[]{"ニシノフラワー","西野花","Nishino Flower"}},
            {1052,new[]{"ハルウララ","春乌菈菈","Haru Urara"}},
            {1053,new[]{"バンブーメモリー","青竹回忆","Bamboo Memory"}},
            {1054,new[]{"ビコーペガサス","微光飞驹","Biko Pegasus"}},
            {1055,new[]{"マーベラスサンデー","美丽周日","Marvelous Sunday"}},
            {1056,new[]{"マチカネフクキタル","待兼福来","Matikanefukukitaru"}},
            {1057,new[]{"ミスターシービー","千明代表","Mr. C.B."}},
            {1058,new[]{"メイショウドトウ","名将怒涛","Meisho Doto"}},
            {1059,new[]{"メジロドーベル","目白多伯","Mejiro Dober"}},
            {1060,new[]{"ナイスネイチャ","优秀素质","Nice Nature"}},
            {1061,new[]{"キングヘイロー","帝王光辉","King Halo"}},
            {1062,new[]{"マチカネタンホイザ","待兼诗歌剧","Matikanetannhauser"}},
            {1063,new[]{"イクノディクタス","生野狄杜斯","Ikuno Dictus"}},
            {1064,new[]{"メジロパーマー","目白善信","Mejiro Palmer"}},
            {1065,new[]{"ダイタクヘリオス","大拓太阳神","Daitaku Helios"}},
            {1066,new[]{"ツインターボ","双涡轮","Twin Turbo"}},
            {1067,new[]{"サトノダイヤモンド","里见光钻","Satono Diamond"}},
            {1068,new[]{"キタサンブラック","北部玄驹","Kitasan Black"}},
            {1069,new[]{"サクラチヨノオー","樱花千代王","Sakura Chiyono O"}},
            {1070,new[]{"シリウスシンボリ","天狼星象征","Sirius Symboli"}},
            {1071,new[]{"メジロアルダン","目白阿尔丹","Mejiro Ardan"}},
            {1072,new[]{"ヤエノムテキ","八重无敌","Yaeno Muteki"}},
            {1073,new[]{"ツルマルツヨシ","鹤丸刚志","Tsurumaru Tsuyoshi"}},
            {1074,new[]{"メジロブライト","目白光明","Mejiro Bright"}},
            {1076,new[]{"サクラローレル","樱花桂冠","Sakura Laurel"}},
            {1077,new[]{"ナリタトップロード","成田路","Narita Top Road"}},
            {1078,new[]{"ヤマニンゼファー","山人西风","Yamanin Zephyr"}},
            {1079,new[]{"フリオーソ","狂热激昂","Furioso"}},
            {1080,new[]{"トランセンド","创升","Transcend"}},
            {1081,new[]{"エスポワールシチー","希望之城","Espoir City"}},
            {1082,new[]{"ノースフライト","北方飞翔","North Flight"}},
            {1083,new[]{"シンボリクリスエス","吉兆","Symboli Kris S"}},
            {1084,new[]{"タニノギムレット","谷野美酒","Tanino Gimlet"}},
            {1085,new[]{"ダイイチルビー","第一红宝","Daiichi Ruby"}},
            {1086,new[]{"メジロラモーヌ","目白拉莫奴","Mejiro Ramonu"}},
            {1087,new[]{"アストンマーチャン","真弓快车","Aston Machan"}},
            {1088,new[]{"サトノクラウン","里见皇冠","Satono Crown"}},
            {1089,new[]{"シュヴァルグラン","高尚骏逸","Cheval Grand"}},
            {1090,new[]{"ヴィルシーナ","极峰","Verxina"}},
            {1091,new[]{"ヴィブロス","强击","Vivlos"}},
            {1092,new[]{"ダンツフレーム","烈焰快驹","Dantsu Flame"}},
            {1093,new[]{"ケイエスミラクル","凯斯奇迹","K.S.Miracle"}},
            {1094,new[]{"ジャングルポケット","森林宝穴","Jungle Pocket"}},
            {1095,new[]{"ビリーヴ","信念","Believe"}},
            {1096,new[]{"ノーリーズン","莫名其妙","No Reason"}},
            {1097,new[]{"スティルインラブ","爱如往昔","Still in Love"}},
            {1098,new[]{"コパノリッキー","小林历奇","Copano Rickey"}},
            {1099,new[]{"ホッコータルマエ","北港火山","Hokko Tarumae"}},
            {1100,new[]{"ワンダーアキュート","奇锐骏","Wonder Acute"}},
            {1102,new[]{"サウンズオブアース","万籁争鸣","Sounds of Earth"}},
            {1103,new[]{"ロイスアンドロイス","莱斯莱斯","Royce and Royce"}},
            {1104,new[]{"カツラギエース","葛城王牌","Katsuragi Ace"}},
            {1105,new[]{"ネオユニヴァース","新宇宙","Neo Universe"}},
            {1106,new[]{"ヒシミラクル","菱钻奇宝","Hishi Miracle"}},
            {1107,new[]{"タップダンスシチー","跳舞城","Tap Dance City"}},
            {1108,new[]{"ドゥラメンテ","大鸣大放","Duramente"}},
            {1109,new[]{"ラインクラフト","莱茵力量","Rhein Kraft"}},
            {1110,new[]{"シーザリオ","西沙里奥","Cesario"}},
            {1111,new[]{"エアメサイア","空中弥赛亚","Air Messiah"}},
            {1112,new[]{"デアリングハート","勇敢之心","Daring Heart"}},
            {1113,new[]{"フサイチパンドラ","火神","Fusaichi Pandora"}},
            {1114,new[]{"ブエナビスタ","迷人景致","Buena Vista"}},
            {1115,new[]{"オルフェーヴル","黄金巨匠","Orfevre"}},
            {1116,new[]{"ジェンティルドンナ","贵妇人","Gentildonna"}},
            {1117,new[]{"ウインバリアシオン","凯旋芭蕾","Win Variation"}},
            {1118,new[]{"アドマイヤグルーヴ","爱慕律动","Admire Groove"}},
            {1119,new[]{"ドリームジャーニー","梦之旅","Dream Journey"}},
            {1120,new[]{"カルストンライトオ","金镇之光","Calstone Light O"}},
            {1121,new[]{"デュランダル","多旺达","Durandal"}},
            {1124,new[]{"バブルガムフェロー","吹波糖","Bubble Gum Fellow"}},
            {1127,new[]{"フェノーメノ","超常骏骥","Fenomeno"}},
            {1129,new[]{"アーモンドアイ","杏目","Almond Eye"}},
            {1130,new[]{"ラッキーライラック","旺紫丁","Lucky Lilac"}},
            {1131,new[]{"グランアレグリア","放声欢呼","Gran Alegria"}},
            {1132,new[]{"ラヴズオンリーユー","唯独爱你","Loves Only You"}},
            {1133,new[]{"クロノジェネシス","创世驹","Chrono Genesis"}},
            {1134,new[]{"カレンブーケドール","机伶金花","Curren Bouquetd'or"}},
            {1135,new[]{"ステイゴールド","黄金旅程","Stay Gold"}},
            {1136,new[]{"レッドディザイア","红色梦想","Red Desire"}},
            {1137,new[]{"キセキ","神业","Kiseki"}},
            {1141,new[]{"エピファネイア","神威启示","Epiphaneia"}},
            {1143,new[]{"ヴィクトワールピサ","比萨胜驹","Victoire Pisa"}},
            {1144,new[]{"ローズキングダム","玫瑰帝国","Rose Kingdom"}},
            {1145,new[]{"ルーラーシップ","统治地位","Rulership"}},
            {1149,new[]{"ファレノプシス","蝴蝶兰","Phalaenopsis"}},
            {9001,new[]{"駿川たづな","骏川手纲","Tazuna Hayakawa"}}
        };
        private static readonly int[] LongestFirst=Names.Keys.OrderByDescending(id=>Names[id][0].Length).ToArray();
        public static string Get(int id,string fallback,string language)
        {
            string[] names;if(!Names.TryGetValue(id,out names))return fallback??"";
            return names[language=="zh"?1:language=="en"?2:0];
        }
        public static bool Matches(int id,string fallback,string query)
        {
            query=(query??"").Trim().Normalize(NormalizationForm.FormKC);
            if(query.Length==0)return true;
            var aliases=new[]{fallback??"",id.ToString(System.Globalization.CultureInfo.InvariantCulture),Get(id,fallback,"ja"),Get(id,fallback,"zh"),Get(id,fallback,"en")};
            return aliases.Any(s=>s.Normalize(NormalizationForm.FormKC).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);
        }
        public static string LocalizeMessage(string message,string language)
        {
            if(string.IsNullOrEmpty(message)||language=="ja")return message;
            // Loaded messages carry the Japanese costume after this separator.
            // Only translate the character segment; never rewrite outfit text.
            int separator=message.IndexOf(" · ",StringComparison.Ordinal);
            string text=separator<0?message:message.Substring(0,separator),tail=separator<0?"":message.Substring(separator);
            foreach(int id in LongestFirst)text=text.Replace(Names[id][0],Get(id,Names[id][0],language));
            return text+tail;
        }
    }
}
