using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Windows.Threading;
using System.IO;
using System.Threading;

namespace BeeCoin
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Task _initializingTask;
        private async Task Init()
        {
            /*
            Initialization that you need with await/async stuff allowed
            */

            MouseDown += Window_MouseDown;
            Grid_all_hide();
            Grid_home.Visibility = Visibility.Visible;
            await Initialize();
        }

        public MainWindow()
        {
            InitializeComponent();
            _initializingTask = Init();
        }

        public async Task<int> DoSomethingWithUIAsync()
        {
            await Task.Delay(15000);
            await HomeGridInit();
            return 42;
        }

        public async void WatchDog()
        {
            while (true)
            {
                var x = await Application.Current.Dispatcher.Invoke<Task<int>>(DoSomethingWithUIAsync);
                Debug.Print(x.ToString()); // prints 42
            }
        }

        public MainClass main = new MainClass();

        public async Task Initialize()
        {
            try
            {
                Grid_home.Visibility = Visibility.Hidden;
                await main.Initialize(this);
                await CheckLogin();
                //await Task.Run(() => WatchDog());
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }


        //
        // GLOBAL VALUES
        //
        public string username;
        public bool admin = false;
        private string root_private;
        public string public_key;
        public string private_key;
        public List<Additional.Transaction> coins = new List<Additional.Transaction>();


        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        public void WriteLine(string text)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RichTextBox_Console.AppendText("[Console]: " + text);
            }), DispatcherPriority.Background);
            text = text + "\r\n";
            byte[] temp = Encoding.UTF8.GetBytes(text);

            main.filesystem.AddInfoToFileAsync(main.filesystem.FSConfig.temp_path + @"\log.txt", temp, false);
        }

        #region MENU AND TITLE
        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            // Close this window
            this.Close();
        }
        private void Button_Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Button_hamburger_Click(object sender, RoutedEventArgs e)
        {
            double wide = 150;
            double thin = 50;
            double current = Grid_Content.ColumnDefinitions[0].Width.Value;

            if (current == thin)
                current = wide;
            else
                current = thin;

            Grid_Content.ColumnDefinitions[0].Width = new GridLength(current, GridUnitType.Pixel);
        }

        private void Grid_all_hide()
        {
            Grid_home.Visibility = Visibility.Hidden;
            Grid_transactions.Visibility = Visibility.Hidden;
            Grid_blocks.Visibility = Visibility.Hidden;
            Grid_admin.Visibility = Visibility.Hidden;
            Grid_settings.Visibility = Visibility.Hidden;
        }

        private async void Button_home_Click(object sender, RoutedEventArgs e)
        {
            Grid_all_hide();
            Grid_home.Visibility = Visibility.Visible;
            await HomeGridInit();
        }

        
        private void Button_transaction_Click(object sender, RoutedEventArgs e)
        {
            Grid_all_hide();
            Grid_transactions.Visibility = Visibility.Visible;

        }
        private void Button_blocks_Click(object sender, RoutedEventArgs e)
        {
            Grid_all_hide();
            Grid_blocks.Visibility = Visibility.Visible;
        }
        private void Button_settings_Click(object sender, RoutedEventArgs e)
        {
            Grid_all_hide();
            Grid_settings_Lable_version.Content = main.info.version;
            Grid_settings.Visibility = Visibility.Visible;
        }
        #endregion

        #region LOGIN
        public async Task CheckLogin()
        {
            string wallet_path = main.filesystem.FSConfig.root_path + @"\wallet";

            if (File.Exists(wallet_path))
            {
                List<string> wallet = await main.cryptography.OpentheWallet(wallet_path);
                username = wallet[0];
                private_key = wallet[1];
                public_key = wallet[2];

                WriteLine("Username: " + username);
                Label_Grid_home_username.Content = username;
                public_key = main.cryptography.public_key;

                Grid_login.Visibility = Visibility.Hidden;
                await main.StartAll();
                Grid_home.Visibility = Visibility.Visible;

                await HomeGridInit();
            }
            else
            {
                Grid_login.Visibility = Visibility.Visible;
            }

            CheckAdmin();
        }

        private async void Button_Login_Click(object sender, RoutedEventArgs e)
        {
            if ((TextBox_username.Text.Length < 4) || (TextBox_username.Text.Length > 16))
            {
                Label_login_status.Content = "Пожалуйста, используйте логин длиной от 4 до 16";
            }
            else
            {
                username = TextBox_username.Text.ToString();
                string wallet_path = main.filesystem.FSConfig.root_path;
                await main.cryptography.MaketheWallet(wallet_path, username);
                await CheckLogin();
            }
            await HomeGridInit();
        }
        #endregion

        #region UPDATE
        public byte[] update;

        private void Button_Update_Click(object sender, RoutedEventArgs e)
        {
            main.update.StartUpdateSelf(update);
        }

        public void ShowUpdateAvailable(string version, byte[] update_data)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Grid_Update_Label_info.Content += version;

                float size = update_data.Length / 1024;
                Math.Round(size, 2);
                update = update_data;

                Grid_Update_Label_info.Content += " (" + size.ToString() + "Kb)";
                Grid_Update.Visibility = Visibility.Visible;
            }), DispatcherPriority.Background);
        }
        #endregion

        #region ADMIN

        public void CheckAdmin()
        {
            string root_path = main.filesystem.FSConfig.root_path + @"\root_private.xml";

            if (File.Exists(root_path))
            {
                string test = "test";
                byte[] temp = new byte[0];
                byte[] sign_temp = new byte[0];
                root_private = string.Empty;

                temp = Encoding.UTF8.GetBytes(test);
                root_private = main.filesystem.ReadAllTextFromFileAsync(root_path);

                sign_temp = main.admin.MakeAdminSignature(temp, root_private);

                if (main.admin.CheckAdminSignature(temp, sign_temp))
                {
                    Button_admin.Visibility = Visibility.Visible;
                    admin = true;
                }
            }
        }

        private void Button_admin_Click(object sender, RoutedEventArgs e)
        {
            Grid_all_hide();
            Grid_admin.Visibility = Visibility.Visible;
        }

        private async void Button_admin_update_all_ClickAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                await main.admin.DirectControlCommand(null, "update", root_private);
            }
            catch (Exception ex)
            {
                WriteLine(ex + "\n");
            }
        }

        private async void Button_admin_sign_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await main.admin.SignCurrentVersion(root_private);
                await main.info.ActualizeSelfSignature();
            }
            catch (Exception ex)
            { 
                WriteLine(ex.ToString());
            }
        }

        private async void Button_admin_money_Click(object sender, RoutedEventArgs e)
        {
            Additional.Transaction transaction = new Additional.Transaction();
            transaction.version = 1;
            transaction.input.put = main.cryptography.HashToString(main.cryptography.GetSHA256Hash(Encoding.UTF8.GetBytes("admin")));
            transaction.input.value = 1;
            transaction.output.put = main.cryptography.GetHashString(public_key);
            transaction.output.value = 1;
            transaction.public_key = Information.admin_public_key;
            transaction.information = Encoding.UTF8.GetBytes("Подарок на день рождения~!!!!!!");

            string name = await main.transactions.MakeNewTransaction(transaction, root_private);

            WriteLine("[admin]: added 1 beecoin (" + name + ") to this account: " + main.cryptography.GetHashString(public_key));
        }

        private async void Button_admin_test_Click(object sender, RoutedEventArgs e)
        {
            await main.blocks.BlockCreate(true);
        }
        #endregion

        #region Settings

        private async void Button_check_for_update_Click(object sender, RoutedEventArgs e)
        {
            await main.update.CheckForUpdate();
        }





        #endregion

        #region HOME

        public async Task HomeGridInit()
        {
            int temp = 0;
            Home_Local_Blocks_Count.Content = main.CountLocalBlocks();
            Home_Global_Blocks_Count.Content = Home_Local_Blocks_Count.Content;
            temp = main.CountLocalTransactions();
            Home_Local_Transacions_Count.Content = temp;
            Home_Global_Transactions_Count.Content = await main.CountGlobalTransactions();
            Home_status_wallet.Text = main.cryptography.HashToString(main.cryptography.GetSHA256Hash(Encoding.UTF8.GetBytes(public_key)));

            coins = await main.transactions.GetTransactionsListToPublicKey(main.cryptography.HashToString(main.cryptography.GetSHA256Hash(Encoding.UTF8.GetBytes(public_key))));

            Wallet_Coins.Content = coins.Count;

            if (temp == 0)
                Home_make_block.IsEnabled = false;
            else
                Home_make_block.IsEnabled = true;
        }

        public void Switch_status(int status)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                switch (status)
                {
                    case 0:
                        Home_Status.Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0)); //крсный
                        Home_Status.Content = "Не в сети";
                        break;
                    case 1:
                        Home_Status.Foreground = new SolidColorBrush(Color.FromRgb(11, 122, 0)); //зеленый
                        Home_Status.Content = "Ограничено";
                        break;
                    case 2:
                        Home_Status.Foreground = new SolidColorBrush(Color.FromRgb(11, 122, 0)); //зеленый
                        Home_Status.Content = "В сети";
                        break;
                    default:
                        break;
                }
            }), DispatcherPriority.Background);
        }

        private async void Home_make_block_Click(object sender, RoutedEventArgs e)
        {
            int temp = main.CountLocalTransactions();
            if (temp != 0)
                temp = await main.blocks.BlockCreate();
            await HomeGridInit();
        }

        #endregion

        #region TRANSACTION

        private async void Transaction_Add_choice_Click(object sender, RoutedEventArgs e)
        {
            Grid_Transaction_Add.Visibility = Visibility.Visible;
            Grid_Transaction_Get.Visibility = Visibility.Hidden;
            Transaction_Add_publickey.Text = public_key;

            await UpdateTransaction_Add_input();
        }

        private void Transaction_Get_choice_Click(object sender, RoutedEventArgs e)
        {
            Grid_Transaction_Add.Visibility = Visibility.Hidden;
            Grid_Transaction_Get.Visibility = Visibility.Visible;
        }

        #region TRANSACTION - ADD

        private async Task UpdateTransaction_Add_input()
        {
            Transaction_Add_input.Items.Clear();

            coins = await main.transactions.GetTransactionsListToPublicKey(main.cryptography.HashToString(main.cryptography.GetSHA256Hash(Encoding.UTF8.GetBytes(public_key))));

            foreach (Additional.Transaction coin in coins)
            {
                Transaction_Add_input.Items.Add(coin.name);
            }
        }

        private async void Transaction_sign_Click(object sender, RoutedEventArgs e)
        {
            Additional.Transaction transaction = new Additional.Transaction();

            transaction.input.put = Transaction_Add_input.SelectedValue.ToString();

            string input = Transaction_Add_input.SelectedValue.ToString();
            string output = Transaction_Add_output.Text;
            string information = transaction_Add_information.Text;
            string name;

            bool trigger = false;

            if ((input.Length == 64) && (output.Length == 64) && (information.Length <= 128))
            {
                trigger = false;

                foreach (Additional.Transaction new_coin in coins)
                {
                    if (new_coin.name == input)
                        trigger = true;
                }
                
                if(trigger)
                {
                    transaction.version = 1;
                    transaction.input.put = input;
                    transaction.input.value = 1;
                    transaction.output.put = output;
                    transaction.output.value = 1;
                    transaction.information = Encoding.UTF8.GetBytes(information);
                    transaction.public_key = public_key;
                    name = await main.transactions.MakeNewTransaction(transaction, private_key);

                    Transaction_Add_Result.Text = name;
                    Transaction_Add_info.Content = "Транзакция успешно создана.";
                }
            }
            else
            {
                WriteLine(input + " " + input.Length);
                WriteLine(output + " " + output.Length);
                WriteLine(information + " " + information.Length);

                Transaction_Add_Warning.Content = "Проверьте, пожалуйста, вводимые значения.";
            }

            coins = await main.transactions.GetTransactionsListToPublicKey(main.cryptography.HashToString(main.cryptography.GetSHA256Hash(Encoding.UTF8.GetBytes(public_key))));


        }

        private async void Transaction_Add_Update_Click(object sender, RoutedEventArgs e)
        {
            Transaction_Add_input.Items.Clear();

            coins = await main.transactions.GetTransactionsListToPublicKey(main.cryptography.HashToString(main.cryptography.GetSHA256Hash(Encoding.UTF8.GetBytes(public_key))));

            foreach (Additional.Transaction coin in coins)
            {
                Transaction_Add_input.Items.Add(coin.name);
            }
        }
        #endregion

        #region TRANSACTION - GET

        private async void Transaction_search_Click(object sender, RoutedEventArgs e)
        {

            if (Transaction_Get_Search.Text.Length == 64)
            {
                Additional.Transaction transaction = new Additional.Transaction();

                string name = Transaction_Get_Search.Text;

                transaction = await main.transactions.Search(name);

                if (transaction.signature != null)
                {

                    Transaction_Get_input.Text = transaction.input.put;
                    Transaction_Get_output.Text = transaction.output.put;
                    Transaction_Get_publickey.Text = transaction.public_key;
                    Transaction_Get_information.Text = Encoding.UTF8.GetString(transaction.information);

                    if (transaction.status)
                        Transaction_Get_info.Content = "Транзакция подтверждена";
                    else
                        Transaction_Get_info.Content = "Транзакция не подтверждена";

                    Transaction_Get_Warning.Content = "Информация о транзакции " + name;
                }
                else
                    Transaction_Get_Warning.Content = "Транзакция не найдена";
            }
            else
                Transaction_Get_Warning.Content = "Проверьте форму ввода";
        }


        #endregion

        #endregion

        #region BLOCK

        private async void Blocks_search_Click(object sender, RoutedEventArgs e)
        {
            string search = Blocks_Get_Search.Text;

            if(search.Length == 64)
            {
                byte[] data = await main.blocks.SearchBlock(search, false);

                if (data.Length != 0)
                {
                    Additional.Block block = new Additional.Block();

                    block = main.blocks.BlockDeSerialize(data);

                    Blocks_version.Content = block.version.ToString() + ".0";
                    Blocks_Get_flowing.Text = block.flowing.ToString();
                    Blocks_Get_previous.Text = block.previous;
                    Blocks_Get_time.Text = block.time.ToString();
                    Blocks_Get_transaction_count.Text = block.transactions_count.ToString();

                    List<string> transactions = new List<string>();
                    transactions.AddRange(block.transactions_info.Split('|'));
                    Blocks_Get_transactions.Items.Clear();
                    foreach (string transaction in transactions)
                    {
                        Blocks_Get_transactions.Items.Add(transaction);
                    }
                    
                }
                else
                {
                    Blocks_Get_Warning.Content = "Блок не найден";
                }
            }
            else
            {
                Blocks_Get_Warning.Content = "Проверьте вводимы данные";
            }
            /*public string name;
        public int version;
        public int time;
        public string previous;
        public int transactions_count;
        public string root_hash;
        public int transactions_info_size;
        public string transactions_info;
        public UInt64 flowing;
*/
    }
    #endregion
}
}
