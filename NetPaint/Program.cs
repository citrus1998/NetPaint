using System.Windows.Forms;

namespace NetPaint   // この行は変更禁止（提出レポートの識別に利用するため）
{
    class Program : System.Windows.Forms.Form
    {
        private System.Drawing.Image Canvas;    // 絵を描くためのキャンバス（画像データ）
        private System.Windows.Forms.TextBox ServerBox; // サーバのホスト名またはIPアドレスを入力するテキストボックス
        private System.Windows.Forms.Button ConnectButton;  // 接続ボタン
        private System.Net.Sockets.Socket Sock; // サーバと通信するためのソケット
        private System.Windows.Forms.TextBox RadiusBox; //半径を入力するテキストボックス
        private System.Windows.Forms.Button RadiusButton;  // 接続ボタン
        private System.Windows.Forms.TextBox ColorBox; //色を入力するテキストボックス
        private System.Windows.Forms.Button ColorButton;  // 色ボタン

        // サーバから受信したデータ等を管理するためのクラス
        class AsyncStateObject
        {
            public System.Net.Sockets.Socket Sock;      // サーバと通信するためのソケット
            public byte[] ReceiveBuffer;                // サーバから受信したデータを一時的に格納するバッファ
            public System.IO.MemoryStream ReceivedData; // サーバから受信したデータを蓄積するメモリストリーム

            // コンストラクタ
            public AsyncStateObject(System.Net.Sockets.Socket sock)
            {
                Sock = sock;
                ReceiveBuffer = new byte[1024];
                ReceivedData = new System.IO.MemoryStream();
            }
        }

        // サーバからデータを受信したときに呼び出されるメソッド
        private void ReceiveDataCallback(System.IAsyncResult ar)
        {
            AsyncStateObject so = (AsyncStateObject)ar.AsyncState;  // サーバから受信したデータ等を管理するための状態オブジェクト
            int len = so.Sock.EndReceive(ar);     // 今回サーバから受信したデータのサイズ

            // サーバから切断されたときの処理
            if (len <= 0)
            {
                so.Sock.Close();
                return;
            }

            // 今回受信したデータをメモリストリームに蓄積
            so.ReceivedData.Write(so.ReceiveBuffer, 0, len);

            // データの受信が完了したときの処理
            if (so.Sock.Available == 0)
            {
                string str = System.Text.Encoding.UTF8.GetString(so.ReceivedData.ToArray()); // サーバから受信した文字列

                // メモリストリームに蓄積されたデータを消去
                so.ReceivedData.Close();
                so.ReceivedData = new System.IO.MemoryStream();

                lock (Canvas)   // Canvas が他のスレッドから同時に参照されることのないように排他制御
                {
                    System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(Canvas);  // Canvas への描画のためのオブジェクト

                    // たとえばサーバから受信した文字列が "12,34\n56,78\n" であったときは，座標 (12,34) と (56,78) に点（小さい円）を描画
                    string[] messages = str.Split('\n');    // 受信した文字列 str を "\n" で分割したものを文字列配列 messages に格納 
                    foreach (string message in messages)    // 文字列配列 messages の各要素 message について処理
                    {
                        string[] values = message.Split(',');   // 座標の文字列を "," で分割（values[0] がX座標，values[1] がY座標, values[2]が半径となる）
                        if (values.Length != 4)     // 分割後の要素数が 4 でなければ，文字列の形式が不正なので処理をスキップ（通常は起こらない）
                            continue;

                        int drawX = int.Parse(values[0]);   // X座標を int 型に変換
                        int drawY = int.Parse(values[1]);   // Y座標を int 型に変換
                        int radius = int.Parse(values[2]);  // 描画する点（小さい円）の半径

                        // 点（小さい円）を描画
                        if ( values[3] == "Black") g.FillEllipse(System.Drawing.Brushes.Black, drawX - radius, drawY - radius, radius * 2, radius * 2);
                        else if (values[3] == "Blue") g.FillEllipse(System.Drawing.Brushes.Blue, drawX - radius, drawY - radius, radius * 2, radius * 2);
                        else if (values[3] == "Pink") g.FillEllipse(System.Drawing.Brushes.Pink, drawX - radius, drawY - radius, radius * 2, radius * 2);
                        else if (values[3] == "Green") g.FillEllipse(System.Drawing.Brushes.Green, drawX - radius, drawY - radius, radius * 2, radius * 2);
                        else if (values[3] == "Red") g.FillEllipse(System.Drawing.Brushes.Red, drawX - radius, drawY - radius, radius * 2, radius * 2);
                        else g.FillEllipse(System.Drawing.Brushes.Black, drawX - radius, drawY - radius, radius * 2, radius * 2);
                    }
                }

                // 画面を描画（OnPaint() が呼び出される）
                Invalidate();
            }

            // サーバからのデータの待ち受けを再び開始
            so.Sock.BeginReceive(so.ReceiveBuffer, 0, so.ReceiveBuffer.Length,
                System.Net.Sockets.SocketFlags.None, new System.AsyncCallback(ReceiveDataCallback), so);
        }

        // サーバからのデータの待ち受けを開始（データ受信時に ReceiveDataCallback() が呼び出されるようにする）
        private void StartReceive(System.Net.Sockets.Socket sock)
        {
            // サーバから受信したデータ等を管理するための状態オブジェクトを生成
            AsyncStateObject so = new AsyncStateObject(sock);

            sock.BeginReceive(so.ReceiveBuffer, 0, so.ReceiveBuffer.Length,
                System.Net.Sockets.SocketFlags.None, new System.AsyncCallback(ReceiveDataCallback), so);
        }

        // 接続ボタンがクリックされたときに呼び出されるメソッド
        private void ConnectButton_Click(object sender, System.EventArgs e)
        {
            // サーバのホスト名またはIPアドレスを ServerBox.Text，ポート番号を 7777 とする
            System.Net.IPAddress hostadd = System.Net.Dns.GetHostAddresses(ServerBox.Text)[0];
            System.Net.IPEndPoint ephost = new System.Net.IPEndPoint(hostadd, 7777);

            // サーバと通信するためのソケットを作成
            Sock = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

            // サーバに接続
            Sock.Connect(ephost);

            // 接続状態に移行
            ConnectButton.Enabled = false;      // 接続ボタンを無効化
            RadiusButton.Enabled = true;

            // サーバからのデータの待ち受けを開始
            StartReceive(Sock);
        }

        // 画面を描画するときに呼び出されるメソッド
        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            base.OnPaint(e);

            lock (Canvas)   // Canvas が他のスレッドから同時に参照されることのないように排他制御
            {
                // Canvas に描かれた絵を画面に描画
                e.Graphics.DrawImage(Canvas, 0, 0);
            }
        }

        // マウスカーソルが動いたときに呼び出されるメソッド
        protected override void OnMouseMove(System.Windows.Forms.MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // サーバに接続されている状態でマウスの左ボタンが押されているとき
            if (Sock != null && Sock.Connected && e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // サーバに送信する文字列 "マウスX座標,マウスY座標\n" を byte 配列に変換して reqBytes に格納
                byte[] reqBytes = System.Text.Encoding.UTF8.GetBytes(e.X + "," + e.Y + "," + RadiusBox.Text + "," + ColorBox.Text +"\n");

                // サーバにデータ reqBytes を送信
                Sock.Send(reqBytes, reqBytes.Length, System.Net.Sockets.SocketFlags.None);
            }
        }

        public Program()
        {
            // ウィンドウに関する設定
            Text = "ネットペイント";
            ClientSize = new System.Drawing.Size(500, 500);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;

            // サーバのホスト名またはIPアドレスを入力するテキストボックスを配置
            ServerBox = new System.Windows.Forms.TextBox();
            ServerBox.Text = "127.0.0.1"; //192.168.12.30
            ServerBox.Location = new System.Drawing.Point(10, 10);
            ServerBox.Size = new System.Drawing.Size(200, 20);
            Controls.Add(ServerBox);

            //半径を決定するテキストボックス
            RadiusBox = new System.Windows.Forms.TextBox();
            RadiusBox.Text = "5";
            RadiusBox.Location = new System.Drawing.Point(10, 40);
            RadiusBox.Size = new System.Drawing.Size(40, 20);
            Controls.Add(RadiusBox);

            // 接続ボタンを配置
            ConnectButton = new System.Windows.Forms.Button();
            ConnectButton.Text = "接続";
            ConnectButton.Location = new System.Drawing.Point(240, 10);
            ConnectButton.Size = new System.Drawing.Size(40, 20);
            ConnectButton.Click += new System.EventHandler(ConnectButton_Click);
            Controls.Add(ConnectButton);

            //半径ボタンを設置
            RadiusButton = new System.Windows.Forms.Button();
            RadiusButton.Text = "変更";
            RadiusButton.Location = new System.Drawing.Point(60, 40);
            RadiusButton.Size = new System.Drawing.Size(40, 20);
            RadiusButton.Click += new System.EventHandler(ConnectButton_Click);
            Controls.Add(RadiusButton);

            //色のテキストボックス
            ColorBox = new System.Windows.Forms.TextBox();
            ColorBox.Text = "Black";
            ColorBox.Location = new System.Drawing.Point(120, 40);
            ColorBox.Size = new System.Drawing.Size(40, 20);
            Controls.Add(ColorBox);

            //色変更ボタン
            ColorButton = new System.Windows.Forms.Button();
            ColorButton.Text = "変更";
            ColorButton.Location = new System.Drawing.Point(240, 40);
            ColorButton.Size = new System.Drawing.Size(40, 20);
            ColorButton.Click += new System.EventHandler(ConnectButton_Click);
            Controls.Add(ColorButton);

            // Canvas を利用した画面描画を行うための設定
            SetStyle(System.Windows.Forms.ControlStyles.DoubleBuffer
                   | System.Windows.Forms.ControlStyles.UserPaint
                   | System.Windows.Forms.ControlStyles.AllPaintingInWmPaint, true);

            // 絵を描くためのキャンバス（画像データ）を作成
            Canvas = new System.Drawing.Bitmap(ClientSize.Width, ClientSize.Height);
            System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(Canvas);
            g.FillRectangle(System.Drawing.Brushes.White, 0, 0, Canvas.Width, Canvas.Height);
        }

        static void Main(string[] args)
        {
            MessageBox.Show("NET PAINT");
            System.Windows.Forms.Application.Run(new Program());
        }
    }
}
