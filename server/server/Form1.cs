using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static server.Form1;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace server
{
    public partial class Form1 : Form
    {
        //Storing the server and client sockets
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        List<Socket> clientSockets = new List<Socket>();

        bool terminating = false; //closig the server
        bool listening = false;
        bool xTaken = false; //checks if the symbol x is taken by some player
        int numberOfTurns = 0; //used to terminate the game after 9 moves
        int changeIdx = 0; //index after dropping a player
        bool playerSymbolWhoHasTheTurn = true;  //true if it is x's turn

        public struct userInfo // stores user information
        {
            public string nameOfUser;
            public char sembolOfUser;
            public int win;
            public int loss;
            public int tie;

            public userInfo(string name, char symbol, int w, int l, int t) : this()
            {
                nameOfUser = name;
                sembolOfUser = symbol;
                win = w;
                loss = l;
                tie = t;
            }
        }

        List<userInfo> userInfoList = new List<userInfo>(); //list of all users
        List<char> gameList = new List<char>{'1','2','3','4','5','6','7','8','9'}; //the game table
        List<char> resetList = new List<char> { '1', '2', '3', '4', '5', '6', '7', '8', '9' }; //to reset the game table

        public void resetGame(){ //resets the game table 
            for (int i = 0; i < 9; i++) {
                gameList[i] = resetList[i];
            }
        }

        public void broadcast(string message) //broadcast the message to all clients and the spectators
        {
            Byte[] buffer = Encoding.Default.GetBytes(message);
            foreach (Socket cli in clientSockets)
            {
                cli.Send(buffer);   
            }  
        }
        public string printGame() //prints the game table
        {
            string printThis = "";
            for(int i = 0; i < 7 ;i=i+3)
            {
                printThis = printThis + gameList[i] + " | " + gameList[i + 1] + " | " + gameList[i + 2] + "\n";
            }
            return printThis;

        }

        public bool sbWin() //checks all 8 possbilities of somebody winning the game
        {  
            if (gameList[0] == gameList[1] && gameList[1] == gameList[2]) {
                return true;
            }
            else if (gameList[3] == gameList[4] && gameList[4] == gameList[5]) {
                return true;
            }
            else if (gameList[6] == gameList[7] && gameList[7] == gameList[8]) {
                return true;
            }
            else if (gameList[0] == gameList[3] && gameList[3] == gameList[6]) {
                return true;
            }
            else if (gameList[1] == gameList[4] && gameList[4] == gameList[7]) {
                return true;
            }
            else if (gameList[2] == gameList[5] && gameList[5] == gameList[8]) {
                return true;
            }
            else if (gameList[0] == gameList[4] && gameList[4] == gameList[8]) {
                return true;
            }
            else if (gameList[2] == gameList[4] && gameList[4] == gameList[6]) {
                return true;
            }
            else
            {
                return false;
            }
        }

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }

        private void button_listen_Click(object sender, EventArgs e) //starts listening on the given port
        {
            int serverPort;
            if(Int32.TryParse(textBox_port.Text, out serverPort)) //if its a valid port, start listening
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort);
                serverSocket.Bind(endPoint);
                serverSocket.Listen(3);

                listening = true;
                button_listen.Enabled = false;

                Thread acceptThread = new Thread(Accept); //thread to start accepting clients for the given port
                acceptThread.Start();
                Thread waitThread = new Thread(spectatorWait); // to check if client dropped or not
                waitThread.Start();

                logs.AppendText("Started listening on port: " + serverPort + "\n");
            }
            else
            {
                logs.AppendText("Invalid port number, try again.\n");
            }
        }

        private void Accept() //starts accepting clients and giving them their symbols
        {
            while(listening)
            {
                try
                {
                    Socket newClient = serverSocket.Accept();
                    bool flag = false; //checks if x was taken by this specific user
                    
                    try
                    {
                        if (userInfoList.Count() < 4) //can take max 4 players
                        {
                            //get the name input from the users
                            Byte[] buffer = new Byte[64];
                            newClient.Receive(buffer);

                            bool nameTaken = false;
                            string userName = Encoding.Default.GetString(buffer);
                            userName = userName.Substring(0, userName.IndexOf("\0"));
                            foreach (userInfo useri in userInfoList) //check all previously added users to see if name is taken
                            {
                                if (userName == useri.nameOfUser)
                                {
                                    nameTaken = true;
                                    while (nameTaken != false) //keep getting new name until its valid or user disconnects
                                    {
                                        Byte[] buffer3 = Encoding.Default.GetBytes("This user name is taken, please try again.\n");
                                        newClient.Send(buffer3);

                                        Byte[] buffer121 = new Byte[64];
                                        newClient.Receive(buffer121);

                                        userName = Encoding.Default.GetString(buffer121);
                                        userName = userName.Substring(0, userName.IndexOf("\0"));
                                        if (userName != useri.nameOfUser)
                                        {
                                            nameTaken = false; //escape the loop if name is valid
                                        }
                                    }
                                }
                            }
                            logs.AppendText(userName + " is connected.\n");
                            //notify he client
                            Byte[] buffer2 = Encoding.Default.GetBytes("Welcome to the game, " + userName + "!\n");
                            newClient.Send(buffer2);
                            //set the user struct to add to the list
                            userInfo user;
                            user.nameOfUser = userName;
                            user.win = 0;
                            user.loss = 0;
                            user.tie = 0;

                            if ((userInfoList.Count()+1)<=2) {
                                if (xTaken) //give player 1, symbol X 
                                {
                                    user.sembolOfUser = 'O';
                                }
                                else
                                {
                                    user.sembolOfUser = 'X';
                                    xTaken = true;
                                    flag = true;
                                }
                                //send the notifying messages and start the game thread
                                Byte[] buffer4 = Encoding.Default.GetBytes("You are player " + user.sembolOfUser + "\n");
                                newClient.Send(buffer4);

                                userInfoList.Add(user); //add user to the list
                                clientSockets.Add(newClient);
                                logs.AppendText("Player " + user.nameOfUser + " was assigned the symbol: " + user.sembolOfUser + "\n");
                                Thread receiveThread = new Thread(() => startGame(newClient, user));
                                receiveThread.Start();
                            }
                            else //add spectators into the list
                            {
                                logs.AppendText("Player " + user.nameOfUser + " is waiting...\n");
                                Byte[] buffer4 = Encoding.Default.GetBytes("Game is full. You can spectate while you wait :)\n");
                                newClient.Send(buffer4);
                                user.sembolOfUser = 'W';
                                userInfoList.Add(user); //add user to the list
                                clientSockets.Add(newClient);
                            }
                        }
                        else { //notify that the server cannot take more than 4 clients
                            logs.AppendText("Server is full. Cannot take new client\n");
                            Byte[] buffer4 = Encoding.Default.GetBytes("Server is full. Please try again later \n");
                            newClient.Send(buffer4);
                            newClient.Close();
                        }
                    }
                    catch {
                        if (!terminating) //if server is OK, means client is gone
                        {
                            //if we made this guy X
                            if(flag == true)
                            {
                                logs.AppendText("Player X has left the game.\n");
                                xTaken = false; //set X as takeable
                            }
                            else //its player Y that left
                            {
                                logs.AppendText("Player O has left the game \n");
                            }
                            if (clientSockets.Contains(newClient)) { //remove the client if he is added
                                userInfoList.RemoveAt(clientSockets.IndexOf(newClient));
                                clientSockets.Remove(newClient);
                                newClient.Close();
                            }
                        }
                    }
                }
                catch
                {
                    if (terminating)//terminate the server
                    {
                        listening = false;
                    }
                    else
                    {
                        logs.AppendText("The socket stopped working.\n");
                    }
                }
            }
        }
        private void startGame(Socket thisClient, userInfo user) //keeps waiting until there are enough players
        {
            bool flag = true; //loop until there are 2 players
            bool first = true;

            while (flag && !terminating)
            {
                try {
                    if (first)
                    {
                        Byte[] buffer5 = Encoding.Default.GetBytes("Game will begin shortly...\n");
                        logs.AppendText(user.nameOfUser + " is waiting..." +"\n");
                        thisClient.Send(buffer5);
                        first = false;
                    }
                    if (userInfoList.Count == 2) //start the game
                    {
                        Byte[] buffer5 = Encoding.Default.GetBytes("Game begins! :)\n" + printGame() + "\n");
                        logs.AppendText( "Player " + user.nameOfUser + " has started playing.\n");
                        thisClient.Send(buffer5);

                        flag = false;
                        //start accepting inputs
                        Thread receiveThread = new Thread(() => Receive(thisClient, user)); // updated
                        receiveThread.Start();
                        int idx3;
                        if (playerSymbolWhoHasTheTurn) //If is was x's turn
                        {
                            if (user.sembolOfUser == 'X') // if this user symbol is X, give him the turn
                            { 
                                idx3 = userInfoList.IndexOf(user);
                            }
                            else //give other guy the turn
                            {
                                idx3 = 1 - userInfoList.IndexOf(user);
                            }
                        }
                        else //If is was O's turn
                        {
                            if (user.sembolOfUser == 'O') // if this user symbol is O, give him the turn
                            {
                                idx3 = userInfoList.IndexOf(user);
                            }
                            else
                            {
                                idx3 = 1 - userInfoList.IndexOf(user);
                            }
                        }
                        thisClient.Send(Encoding.Default.GetBytes(userInfoList[idx3].nameOfUser + " (" + userInfoList[idx3].sembolOfUser + ")'s turn \n"));
                        logs.AppendText(userInfoList[idx3].nameOfUser + " (" + userInfoList[idx3].sembolOfUser + ")'s turn \n");
                    }
                    else {
                        //check if the client is still available
                        thisClient.Send(Encoding.Default.GetBytes(""));
                    }
                }
                 catch
                {
                    if (!terminating) //if somebody left
                    {
                        int idx = clientSockets.IndexOf(thisClient);
                        string s = "";
                        if (userInfoList[idx].sembolOfUser == 'X') //if X left
                        {
                            s = "Player X has left the game.\n";
                            logs.AppendText(s);
                            xTaken = false;
                        }
                        else {
                            s = "Player O has left the game.\n";
                            logs.AppendText(s);
                        }
                        thisClient.Close();
                        userInfoList.RemoveAt(clientSockets.IndexOf(thisClient));
                        clientSockets.Remove(thisClient);
                        //remove the player from all lists
                        broadcast(s); //broadcast the message to the everybody
                        flag = false;
                    }
                }
            }
        }

        private void continueGame(Socket thisClient, userInfo user)  //adds the spectator to the list
        {
            bool flag = true;
            bool first = true;

            while (flag && !terminating)
            {
                try
                {
                    if (first)
                    {
                        Byte[] buffer5 = Encoding.Default.GetBytes("Game will begin shortly...\n");
                        logs.AppendText(user.nameOfUser + " is waiting..." + "\n");
                        thisClient.Send(buffer5);
                        broadcast("A new person has joined the game, show must go on!\n");
                        first = false;
                        int idx = userInfoList.IndexOf(user);
                        userInfo temp = userInfoList[idx];

                        if (xTaken) //assigns the symbols
                        {
                            user.sembolOfUser = 'O';
                            temp.sembolOfUser = 'O';                         
                        }
                        else
                        {
                            user.sembolOfUser = 'X';
                            temp.sembolOfUser = 'X';                            
                            xTaken = true;
                        }
                        userInfoList[idx] = temp;
                        //send the notifying messages and start the game thread
                        Byte[] buffer4 = Encoding.Default.GetBytes("You are player " + user.sembolOfUser + "\n");
                        thisClient.Send(buffer4);
                    }
                    if (userInfoList.Count >= 2) //continue the game
                    {
                        Byte[] buffer5 = Encoding.Default.GetBytes("Game continues! :)\n" + printGame() + "\n");
                        logs.AppendText("Player " + user.nameOfUser + " has started playing.\n");
                        thisClient.Send(buffer5);

                        flag = false;
                        //start accepting inputs
                        Thread receiveThread = new Thread(() => Receive(thisClient, user)); 
                        receiveThread.Start();
                        int idx3;

                        // set the turns similar to above
                        if (playerSymbolWhoHasTheTurn) {//x's turn
                            if (user.sembolOfUser == 'X')
                            { //im x
                                idx3 = userInfoList.IndexOf(user);
                            }
                            else
                            {
                                idx3 = 1 - userInfoList.IndexOf(user);
                            }
                        }
                        else //other 
                        {
                            if (user.sembolOfUser == 'O')
                            {
                                idx3 = userInfoList.IndexOf(user);
                            }
                            else
                            {
                                idx3 = 1 - userInfoList.IndexOf(user);
                            }
                        }
                        broadcast(userInfoList[idx3].nameOfUser + " (" + userInfoList[idx3].sembolOfUser + ")'s turn \n");
                    }
                    else
                    {
                        //check if the client is still available
                        thisClient.Send(Encoding.Default.GetBytes(""));
                    }
                }
                catch
                {
                    if (!terminating) //if somebody left
                    {
                        int idx = clientSockets.IndexOf(thisClient);
                        string s = "";
                        if (userInfoList[idx].sembolOfUser == 'X') //if X left
                        {
                            s = "Player X has left the game.\n";
                            logs.AppendText(s);
                            xTaken = false;
                        }
                        else
                        {
                            s = "Player O has left the game.\n";
                            logs.AppendText(s);
                        }
                        //modify the lists and notify the clients
                        thisClient.Close();
                        userInfoList.RemoveAt(clientSockets.IndexOf(thisClient));
                        clientSockets.Remove(thisClient);
                        //remove the player from all lists
                        broadcast(s);
                        flag = false;
                    }
                }
            }
        }
        private void Receive(Socket thisClient, userInfo user) //start receiving game moves
        {
            bool connected = true;

            while(connected && !terminating)
            {
                try
                {
                    logs.AppendText(user.nameOfUser + "\n");
                    //get the number input from user
                    Byte[] buffer = new Byte[64];
                    thisClient.Receive(buffer);

                    //turn it into a char and find its index on the game table
                    string incomingMessage = Encoding.Default.GetString(buffer);
                    incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));
                    char option = char.Parse(incomingMessage); 
                    int intOption = int.Parse(incomingMessage);

                    logs.AppendText(user.nameOfUser + " has picked " + incomingMessage + "\n");

                    if (gameList[intOption - 1] == option) // if cell was not taken
                    {
                        gameList[intOption - 1] = user.sembolOfUser; //update table with user symbol
                        numberOfTurns++; //increment the turns
                        if (sbWin()) //check if somebody wins with the updated table
                        {
                            logs.AppendText("Game is over! " + user.nameOfUser + " wins!\n");
                            logs.AppendText("Restarting the game...\n");

                            broadcast("Game is over! " + user.nameOfUser + " wins!\n" + printGame());

                            //resetting the game
                            int idx2 = userInfoList.IndexOf(user);
                            int loser = 1 - idx2;
                            //modify win-loss counts for the loser
                            userInfo temp2 = userInfoList[loser];
                            temp2.loss = userInfoList[loser].loss+1;
                            userInfoList[loser] = temp2;

                            //modify win-loss counts for the winner
                            userInfo temp = userInfoList[idx2];
                            temp.win = userInfoList[idx2].win + 1;
                            userInfoList[idx2] = temp;
                            user = temp;
                            changeIdx = loser;
                            //print the results
                            broadcast("|  Name   |   Win   |   Loss  |  Tie  |\n" +
                                "|  " + temp.nameOfUser + "  |  " + temp.win + "  |  " + temp.loss + "  |  " + temp.tie + "  | \n" +
                                     "|  " + temp2.nameOfUser + "  |  " + temp2.win + "  |  " + temp2.loss + "  |  " + temp2.tie + "  | \n");

                            resetGame();
                            numberOfTurns = 0;

                            //its the other guys turn
                            logs.AppendText(userInfoList[loser].nameOfUser + " (" + userInfoList[loser].sembolOfUser + ")'s turn \n");
                            if (user.sembolOfUser == 'X')
                            {
                                playerSymbolWhoHasTheTurn = false;
                            }
                            else
                            {
                                playerSymbolWhoHasTheTurn = true;
                            }
                            //start the new game and notify the users
                            broadcast("A new game begins...\n" + userInfoList[loser].nameOfUser + " (" + userInfoList[loser].sembolOfUser + ")'s turn \n" + printGame());
                        }
                        else
                        {
                            broadcast(printGame());
                            if (numberOfTurns == 9) //if number of turns finished but nobody wins the game
                            {
                                logs.AppendText("It is a tie. Nobody wins\n");
                                broadcast("It is a tie. Nobody wins\n");

                                int idx2 = userInfoList.IndexOf(user);
                                int loser = 1 - idx2;
                                //increment everybody's tie count
                                userInfo temp2 = userInfoList[loser];
                                temp2.tie = userInfoList[loser].tie + 1;
                                userInfoList[loser] = temp2;

                                userInfo temp = userInfoList[idx2];
                                temp.tie = userInfoList[idx2].tie + 1;
                                userInfoList[idx2] = temp;
                                user = temp;
                                changeIdx = loser;

                                broadcast("|  Name   |   Win   |   Loss  |  Tie  |\n" +
                                "|  " + temp.nameOfUser + "  |  " + temp.win + "  |  " + temp.loss + "  |  " + temp.tie + "  | \n" +
                                     "|  " + temp2.nameOfUser + "  |  " + temp2.win + "  |  " + temp2.loss + "  |  " + temp2.tie + "  | \n");

                                resetGame();
                                numberOfTurns = 0;

                                //its the other guys turn
                                logs.AppendText(userInfoList[loser].nameOfUser + " (" + userInfoList[loser].sembolOfUser + ")'s turn \n");
                                if (user.sembolOfUser == 'X')
                                {
                                    playerSymbolWhoHasTheTurn = false;
                                }
                                else
                                {
                                    playerSymbolWhoHasTheTurn = true;
                                }

                                broadcast("A new game begins...\n" + userInfoList[loser].nameOfUser + " (" + userInfoList[loser].sembolOfUser + ")'s turn \n" + printGame());
            
                            }
                            else //game continues
                            {
                                int idx2 = userInfoList.IndexOf(user); 
                                //give the next player the turn
                                if (idx2 < 0) {
                                    idx2 = changeIdx;
                                    user = userInfoList[idx2];
                                }
                                int loser = 1 - idx2;
                                //broadcast the new turns
                                broadcast(userInfoList[loser].nameOfUser + " (" + userInfoList[loser].sembolOfUser + ")'s turn \n");

                                if (user.sembolOfUser=='X')
                                {
                                    playerSymbolWhoHasTheTurn = false;
                                }
                                else
                                {
                                    playerSymbolWhoHasTheTurn = true;
                                }
                                logs.AppendText(userInfoList[loser].nameOfUser + " (" + userInfoList[loser].sembolOfUser + ")'s turn \n");
                            }
                        }
                    }
                    else
                    {
                        logs.AppendText("The cell was already taken.\n");
                        Byte[] buffer4 = Encoding.Default.GetBytes("Cell is already taken, pick something else.\n");
                        thisClient.Send(buffer4);
                    }
                }
                catch
                {
                    if (!terminating) //if somebody left
                    {
                        int idx = clientSockets.IndexOf(thisClient);
                        if (userInfoList[idx].sembolOfUser == 'X') //if X left
                        {
                            logs.AppendText("Player X has left the game.\n");
                            xTaken = false;
                        }
                        else
                        {
                            logs.AppendText("Player O has left the game.\n");
                        }
                       
                        //Remove the guy who left 
                        userInfoList.RemoveAt(clientSockets.IndexOf(thisClient));
                        clientSockets.Remove(thisClient);
                        connected = false;
                        thisClient.Close();
                        //remove the player from all lists
                        broadcast("Somebody has left the game..\n");
                        if (userInfoList.Count()>=2) { //If we have spectators get them into the game
                            Thread receiveThread = new Thread(() => continueGame(clientSockets[1], userInfoList[1]));
                            receiveThread.Start();
                        }              
                    }
                }
            }
        }
        private void spectatorWait() //check if socket is still available or not
        {
            while (listening) //while the server is still on
            {
                try
                {
                    //for all the possible spectators 
                    for (int i = 2; i < clientSockets.Count(); i++)
                    {
                        Socket clientSocket = clientSockets[i];

                        bool isAvailable = clientSocket.Poll(0, SelectMode.SelectRead);

                        // Check if the socket has been closed or in an error state
                        bool isClosed = !isAvailable && clientSocket.Available == 0;

                        if (!isClosed) //if socket is not available, remove the user and broadcast to everyone
                        {
                            string n = userInfoList[i].nameOfUser + " has left the game. \n";
                            userInfoList.RemoveAt(i);
                            clientSockets.RemoveAt(i);
                            clientSocket.Close();
                            //remove the player from all lists
                            broadcast(n);
                            logs.AppendText(n);
                        }
                    }
                }
                catch
                {
                    logs.AppendText("This is an error you shouldn't get");
                }
            }
        }

        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            listening = false;
            terminating = true;
            Environment.Exit(0);
        }
    }
}
