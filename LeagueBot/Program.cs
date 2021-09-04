using Accord.Imaging;
using Accord.Imaging.Filters;
using LeagueLCD;
using LeagueLCD.Models;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput;
using WindowsInput.Native;

namespace LeagueBot
{
    class Point
    {
        public int x;
        public int y;
        public Point(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }
    class Program
    {
        //This is a replacement for Cursor.Position in WinForms
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;

        public const int middle_down = 0x0020;
        public const int middle_up = 0x0040;

        public const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        public const int MOUSEEVENTF_RIGHTUP = 0x10;

        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const int W_KEY = 0x57;

        public static void LeftMouseClick(Point pt)
        {
            SetCursorPos(pt.x, pt.y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, pt.x, pt.y, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, pt.x, pt.y, 0, 0);
        }

        public static void RightMouseClick(Point pt)
        {
            SetCursorPos(pt.x, pt.y);
            mouse_event(MOUSEEVENTF_RIGHTDOWN, pt.x, pt.y, 0, 0);
            mouse_event(MOUSEEVENTF_RIGHTUP, pt.x, pt.y, 0, 0);
        }

        public static void MiddleMouseClick(Point pt)
        {
            SetCursorPos(pt.x, pt.y);
            mouse_event(middle_down, pt.x, pt.y, 0, 0);
            mouse_event(middle_up, pt.x, pt.y, 0, 0);
        }

        public static void KeyPress(int code)
        {
            keybd_event((byte)code,
             0x45,
             KEYEVENTF_EXTENDEDKEY | 0,
             0);

            // Simulate a key release
            keybd_event((byte)code,
                         0x45,
                         KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP,
                         0);
        }

        /// <summary>
        /// Attempts to level up an ability.
        /// </summary>
        /// <param name="code"></param>
        public static void LevelUpAbility(VirtualKeyCode code)
        {
            input.Keyboard.KeyDown(VirtualKeyCode.LCONTROL);
            input.Keyboard.KeyPress(code);
            input.Keyboard.KeyUp(VirtualKeyCode.LCONTROL);
        }

        public static GameConnection con;
        static Point redSpawn = new Point(1893, 828); //new Point(1893, 828);
        static Point blueSpawn = new Point(1671, 1050);
        static Point topRight = new Point(1663, 34);
        static Point bottomLeft = new Point(222, 989);
        static Point leftShopItem = new Point(436, 484);
        static Point purchaseButton = new Point(1149, 858);

        //lobby client points (unused)
        static Point playButton = new Point(440, 200); // top left play button
        static Point coopVsAiButton = new Point(466, 261);
        static Point confirmButton = new Point(861, 849);
        static Point queueAcceptButton = new Point(961, 718);

        static bool inLobby = true;
        static bool inQueue = false;
        static volatile bool queuePopped = false;

        static InputSimulator input = new InputSimulator();
        static Bitmap HealthBarTemplate = new Bitmap(@"D:\LeagueBot\HealthBar.png");
        static Bitmap MinionHealthBarTemplate = new Bitmap(@"D:\LeagueBot\MinionHealthBar.png");
        static Bitmap QueuePopTemplate = new Bitmap(@"D:\LeagueBot\QueuePop.png");
        static ExhaustiveTemplateMatching tm = new ExhaustiveTemplateMatching(0.97f);
        static ExhaustiveTemplateMatching minionMatch = new ExhaustiveTemplateMatching(0.95f);
        static ExhaustiveTemplateMatching queueButtonMatch = new ExhaustiveTemplateMatching(0.97f);
        static Bitmap screen;

        static Point championTarget;
        static Point minionTarget;

        static Thread imageProcessingThread;
        static Thread clientThread;

        public static int TICKRATE = 8;
        static async Task Main(string[] args)
        {
            clientThread = new Thread(mainThread);
            clientThread.Start();
            imageProcessingThread = new Thread(ImageRecognition);
            imageProcessingThread.Start();
            con = new GameConnection();
            //await BotLoop();
            Console.ReadLine();
        }
        public static void mainThread()
        {
            while(true)
            {
                Thread.Sleep(500);
                Process[] processes = Process.GetProcessesByName("League of Legends");
                if (processes.Length == 0) // If the game is closed, try to queue up
                {
                    if (!inQueue)
                    {
                        Console.WriteLine("Starting queue");
                        LeftMouseClick(confirmButton);
                        Thread.Sleep(250);
                        inQueue = true;
                    }
                    else if(queuePopped)
                    {
                        LeftMouseClick(queueAcceptButton);
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to locate all image recognition related targets.
        /// </summary>
        public static void ImageRecognition()
        {
            while (true)
            {
                screen = new Bitmap(1920, 1080, PixelFormat.Format24bppRgb);
                Graphics gr = Graphics.FromImage(screen);
                gr.CopyFromScreen(0, 0, 0, 0, screen.Size);

                if (con.Connected)
                {
                    championTarget = FindTarget();
                    minionTarget = FindMinionTarget();
                }
                if(!con.Connected)
                {
                    FindQueuePop();
                }
            }
        }

        public static void FindQueuePop()
        {
            TemplateMatch[] matchings = queueButtonMatch.ProcessImage(screen, QueuePopTemplate);

            if (matchings.Length > 0)
            {
                queuePopped = true;
            }
            else
            {
                queuePopped = false;
            }
        }

        public static Point FindTarget()
        {
            TemplateMatch[] matchings = tm.ProcessImage(screen, HealthBarTemplate);

            if (matchings.Length > 0)
            { 
                return new Point(matchings[0].Rectangle.X+45, matchings[0].Rectangle.Y + 90);
            }
            else
            {
                return null;
            }
        }

        public static Point FindMinionTarget()
        {
            TemplateMatch[] matchings = minionMatch.ProcessImage(screen, MinionHealthBarTemplate);

            if (matchings.Length > 0)
            {
                return new Point(matchings[0].Rectangle.X, matchings[0].Rectangle.Y + 40);
            }
            else
            {
                return null;
            }
        }
        public static async Task BotLoop()
        {
            Console.WriteLine("Beginning bot loop...");
            float QTimer = 3;
            float WTimer = 4;
            float ETimer = 45;
            float RTimer = 2;
            float retreatTime = 0;
            double healthLastTick = 570;
            int levelLastTick = 0;
            int myPlayerIndex = -1;
            bool deadLastTick = true;

            Thread.Sleep(3000); // Wait for connection

            foreach(AllPlayer p in con.Game.allPlayers) // Look for our player index (character playing Ashe)
            {
                if(p.championName == "Ashe")
                {
                    myPlayerIndex = con.Game.allPlayers.IndexOf(p);
                    break;
                }
            }

            while(true)
            {
                if(con.Connected && con.Game.gameData.gameTime > 70 && con.Game.activePlayer.championStats.currentHealth > 1) // Don't activate til 70 seconds in and not while dead
                {
                    AllPlayer p = con.Game.allPlayers[myPlayerIndex];
                    if((healthLastTick - con.Game.activePlayer.championStats.currentHealth)/(float)con.Game.activePlayer.championStats.maxHealth > 0.15f)
                    {
                        Console.WriteLine("Big damage - retreat for a bit");
                        input.Keyboard.KeyPress(VirtualKeyCode.VK_F);
                        int kitingDone = 0;
                        Console.WriteLine("Start kiting");
                        while (kitingDone < 3000)
                        {
                            RightMouseClick(blueSpawn);
                            Thread.Sleep((int)(1000f / con.Game.activePlayer.championStats.attackSpeed * 0.32f));
                            MiddleMouseClick(blueSpawn);
                            Thread.Sleep((int)(1000f / con.Game.activePlayer.championStats.attackSpeed * 0.68f));
                            kitingDone += (int)(1000f / con.Game.activePlayer.championStats.attackSpeed);
                        }
                        Thread.Sleep(10);
                        //MiddleMouseClick(blueSpawn);
                        //Thread.Sleep(3500);
                    }
                    healthLastTick = con.Game.activePlayer.championStats.currentHealth;
                    // Item buying logic
                    if (deadLastTick) // Buy stuff if we just respawned
                    {
                        input.Keyboard.KeyPress(VirtualKeyCode.VK_P);
                        Thread.Sleep(300);
                        SetCursorPos(leftShopItem.x, leftShopItem.y);
                        Thread.Sleep(300);
                        input.Mouse.LeftButtonDown();
                        Thread.Sleep(50);
                        input.Mouse.LeftButtonUp();
                        Thread.Sleep(300);
                        SetCursorPos(purchaseButton.x, purchaseButton.y);
                        Thread.Sleep(300);
                        input.Mouse.LeftButtonDown();
                        Thread.Sleep(50);
                        input.Mouse.LeftButtonUp();
                        Thread.Sleep(300);
                        input.Keyboard.KeyPress(VirtualKeyCode.VK_P);
                        Thread.Sleep(300);
                        input.Keyboard.KeyPress(VirtualKeyCode.VK_D);
                        deadLastTick = false;
                    }
                    // Levelup logic
                    if(p.level != levelLastTick)
                    {
                        switch(p.level) // TODO: obviously make this an array or something but it works for now
                        {
                            case 1: { LevelUpAbility(VirtualKeyCode.VK_W); break; }
                            case 2: { LevelUpAbility(VirtualKeyCode.VK_Q); break; }
                            case 3: { LevelUpAbility(VirtualKeyCode.VK_W); break; }
                            case 4: { LevelUpAbility(VirtualKeyCode.VK_Q); break; }
                            case 5: { LevelUpAbility(VirtualKeyCode.VK_W); break; }
                            case 6: { LevelUpAbility(VirtualKeyCode.VK_R); break; }
                            case 7: { LevelUpAbility(VirtualKeyCode.VK_W); break; }
                            case 8: { LevelUpAbility(VirtualKeyCode.VK_Q); break; }
                            case 9: { LevelUpAbility(VirtualKeyCode.VK_W); break; }
                            case 10: { LevelUpAbility(VirtualKeyCode.VK_Q); break; }
                            case 11: { LevelUpAbility(VirtualKeyCode.VK_R); break; }
                            case 12: { LevelUpAbility(VirtualKeyCode.VK_Q); break; }
                            case 13: { LevelUpAbility(VirtualKeyCode.VK_E); break; }
                            case 14: { LevelUpAbility(VirtualKeyCode.VK_E); break; }
                            case 15: { LevelUpAbility(VirtualKeyCode.VK_E); break; }
                            case 16: { LevelUpAbility(VirtualKeyCode.VK_R); break; }
                            case 17: { LevelUpAbility(VirtualKeyCode.VK_E); break; }
                            case 18: { LevelUpAbility(VirtualKeyCode.VK_E); break; }
                            default: { break; }
                        }
                    }
                    levelLastTick = p.level;

                    //Movement logic
                    if (con.Game.activePlayer.championStats.currentHealth < con.Game.activePlayer.championStats.maxHealth * 0.30f) // If we're below 35% hp, run
                    {
                        if (retreatTime == 0) // Use heal if low and ghost
                        {
                            input.Keyboard.KeyPress(VirtualKeyCode.VK_F);
                            Thread.Sleep(10);
                            input.Keyboard.KeyPress(VirtualKeyCode.VK_D);
                            Thread.Sleep(10);
                        }
                        retreatTime += 1f / TICKRATE;
                        Console.WriteLine("Retreat");
                        MiddleMouseClick(blueSpawn);
                        Thread.Sleep(50);
                        int kitingDone = 0;
                        Console.WriteLine("Start kiting");
                        while (kitingDone < 10000)
                        {
                            RightMouseClick(blueSpawn);
                            Thread.Sleep((int)(1000f / con.Game.activePlayer.championStats.attackSpeed * 0.35f));
                            MiddleMouseClick(blueSpawn);
                            Thread.Sleep((int)(1000f / con.Game.activePlayer.championStats.attackSpeed * 0.65f));
                            kitingDone += (int)(1000f / con.Game.activePlayer.championStats.attackSpeed);
                        }
                        Console.WriteLine("Start recall");
                        input.Keyboard.KeyPress(VirtualKeyCode.VK_B);
                        Thread.Sleep(12000);
                        deadLastTick = true;
                    }
                    else
                    {
                        retreatTime = 0;
                        Console.WriteLine("Attack");
                        RightMouseClick(redSpawn);
                    }
                    if (WTimer <= 0)
                    {
                        if (minionTarget != null)
                        {
                            Console.WriteLine("Fire W - Minions");
                            SetCursorPos(minionTarget.x, minionTarget.y);
                            input.Keyboard.KeyPress(VirtualKeyCode.VK_W);
                            WTimer = 14;
                        }
                        else if(championTarget != null)
                        {
                            Console.WriteLine("Fire W - Champion");
                            SetCursorPos(championTarget.x, championTarget.y);
                            input.Keyboard.KeyPress(VirtualKeyCode.VK_W);
                            WTimer = 4;
                        }
                    }
                    else
                    {
                        WTimer -= (1f / TICKRATE);
                    }
                    if (QTimer <= 0)
                    {
                        Console.WriteLine("Use Q");
                        input.Keyboard.KeyPress(VirtualKeyCode.VK_Q);
                        QTimer = 7.5f;
                    }
                    else
                    {
                        QTimer -= (1f / TICKRATE);
                    }
                    if (ETimer <= 0)
                    {
                        Console.WriteLine("Fire E");
                        SetCursorPos(redSpawn.x, redSpawn.y);
                        input.Keyboard.KeyPress(VirtualKeyCode.VK_E);
                        ETimer = 16;
                    }
                    else
                    {
                        ETimer -= (1f / TICKRATE);
                    }
                    if (RTimer <= 0)
                    {
                        if (championTarget != null && p.level > 5)
                        {
                            Console.WriteLine("Fire R");
                            SetCursorPos(championTarget.x, championTarget.y);
                            input.Keyboard.KeyPress(VirtualKeyCode.VK_R);
                            RTimer = 3;
                        }
                    }
                    else
                    {
                        RTimer -= (1f / TICKRATE);
                    }
                }
                else
                {
                    deadLastTick = true;
                }

                Thread.Sleep(1000/TICKRATE); // Tick
            }
        }
    }
}
