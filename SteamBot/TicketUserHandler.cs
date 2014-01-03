using SteamKit2;
using System.Collections.Generic;
using SteamTrade;
using System.Threading.Tasks;
using System.Threading;

namespace SteamBot
{
    public class TicketUserHandler : UserHandler
    {
        private int delayCounter = 0;

        public int MetalPutUp, TicketsPutUp, MyMetalPutUp, MyTicketsPutUp;

		private static int TICKET_ID = 725;

        private static int MetalCostPerTicket = 16;
        private static int MetalGivenPerTicket = 13;

        public TicketUserHandler (Bot bot, SteamID sid) : base(bot, sid) {}

        private bool valid;

		public override bool OnFriendAdd () 
        {
            Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Hello, I am an automated trading bot. To get started, offer a trade!");
            return true;
        }

        public override void OnLoginCompleted()
        {
        }

        public override void OnChatRoomMessage(SteamID chatID, SteamID sender, string message)
        {
            Log.Info(Bot.SteamFriends.GetFriendPersonaName(sender) + ": " + message);
            base.OnChatRoomMessage(chatID, sender, message);
        }

        public override void OnFriendRemove () {}

        private void SendMessage(string message, bool tradeWindow)
        {
            if (tradeWindow) Trade.SendMessage(message);
            else Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, message);
        }

        private void HandleChatMessage(string message, bool tradeWindow)
        {
            if (message.ToLower().Equals("help"))
            {
                SendMessage("To view my current prices, say \"prices\".", tradeWindow);
                SendMessage("To check how many items I have, say \"stock\".", tradeWindow);
                SendMessage("To get a link to my owner's profile, say \"owner\".", tradeWindow);
                if (!tradeWindow) SendMessage("To start a trade, request one.", false);
            }
            else if (message.ToLower().Equals("price") || message.ToLower().Equals("prices"))
            {
                SendMessage("Current prices:", tradeWindow);
                SendMessage(FormatMetal(MetalCostPerTicket) + " to buy each ticket", tradeWindow);
                SendMessage(FormatMetal(MetalGivenPerTicket) + " offered for each ticket", tradeWindow);
            }
            else if (message.ToLower().Equals("stock"))
            {
                Bot.GetInventory();
                SendMessage("Current stock:", tradeWindow);
                int count1 = Bot.MyInventory.GetNumItemsByDefindex(TICKET_ID);
                SendMessage(count1.ToString() + " ticket" + (count1 == 1 ? "" : "s"), tradeWindow);
                int count2 = Bot.MyInventory.GetNumItemsByDefindex(5000);
                int count3 = Bot.MyInventory.GetNumItemsByDefindex(5001);
                int count4 = Bot.MyInventory.GetNumItemsByDefindex(5002);
                SendMessage(FormatMetal(count2 + count3 * 3 + count4 * 9) + " (" + count2.ToString() + " scrap, " + count3.ToString() + " reclaimed, " + count4.ToString() + " refined)", tradeWindow);
            }
            else if (message.ToLower().Equals("owner"))
            {
                SendMessage("http://steamcommunity.com/profiles/76561197969822695", tradeWindow);
            }
            else SendMessage("I don't understand what you typed, type 'help' for commands.", tradeWindow);
        }
        
        public override void OnMessage (string message, EChatEntryType type) 
        {
            if (type != EChatEntryType.ChatMsg) return;
            HandleChatMessage(message, false);
        }

        public override bool OnTradeRequest() 
        {
            return true;
        }
        
        public override void OnTradeError (string error) 
        {
            Bot.SteamFriends.SendChatMessage (OtherSID, 
                                              EChatEntryType.ChatMsg,
                                              "Oh, there was an error: " + error + "."
                                              );
            Bot.log.Warn (error);
        }
        
        public override void OnTradeTimeout () 
        {
            Bot.SteamFriends.SendChatMessage (OtherSID, EChatEntryType.ChatMsg,
                                              "Sorry, but you were AFK and the trade was cancelled.");
            Bot.log.Warn ("User was kicked because he was AFK.");
        }
        
        public override void OnTradeInit()
        {
            Bot.log.Success("User started trade: " + OtherSID.ToString());
            Trade.SendMessage("To trade, offer your metal/tickets and wait a moment, I will then add the appropriate items for what you offered.");
            Trade.SendMessage("You can still type 'help' to check available commands.");
        }
        
        public override void OnTradeAddItem (Schema.Item schemaItem, Inventory.Item inventoryItem)
        {
            TradeChecker();
        }
        
        public override void OnTradeRemoveItem (Schema.Item schemaItem, Inventory.Item inventoryItem)
        {
            TradeChecker();
        }

        private void TradeChecker()
        {
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;
            var task = Task.Factory.StartNew(() =>
            {
                delayCounter++;
                token.WaitHandle.WaitOne(2500);
                delayCounter--;
                if (delayCounter == 0)
                {
                    Validate(false);
                }
            });
        }

        public override void OnTradeMessage (string message)
        {
            HandleChatMessage(message, true);
        }

        public override void OnTradeReady (bool ready) 
        {
            //Because SetReady must use its own version, it's important
            //we poll the trade to make sure everything is up-to-date.
            Trade.Poll();
            if (!ready)
            {
                Trade.SetReady (false);
            }
            else
            {
                if(Validate (true))
                {
                    if (MetalPutUp > 0)
                    {
                        int numTickets = MetalPutUp / MetalCostPerTicket;
                        int spareMetal = MetalPutUp % MetalCostPerTicket;
                        if (numTickets == 0)
                        {
                            Trade.SendMessage("You have not added enough metal for a ticket, I will not offer anything in return!");
                        }
                        else if (spareMetal > MyMetalPutUp)
                        {
                            Trade.SendMessage("You have left extra metal in your offer (" + FormatMetal(spareMetal-MyMetalPutUp) + "), and I have no change!");
                        }
                    }
                    try
                    {
                        Trade.SetReady(valid);
                    }
                    catch (SteamTrade.Exceptions.TradeException e)
                    {
                        Trade.SendMessage("Something went wrong:" + e.Message);
                        Trade.SendMessage("Try accepting the trade again, usually that fixes it...");
                    }
                }
            }
        }
        
        public override void OnTradeAccept() 
        {
            if (valid || IsAdmin)
            {
                try
                {
                    Trade.AcceptTrade();
                    SendMessage("Trade completed!", false);
                }
                catch
                {
                    Log.Warn ("The trade might have failed, but we can't be sure.");
                    SendMessage("The trade might have failed, check your inventory for the items!", false);
                }
                SendMessage("You can keep this bot in your friends if you want to trade with it again.", false);
                Log.Success ("Trade Complete!");
            }

            OnTradeClose ();
        }

        public bool Validate (bool reportEmpty)
        {            
            MetalPutUp = 0;
			TicketsPutUp = 0;
            
			List<string> errors = new List<string> ();

            foreach (ulong id in Trade.OtherOfferedItems)
            {
                var item = Trade.OtherInventory.GetItem (id);
                if (item.Defindex == TICKET_ID)
                    TicketsPutUp++;
                else if (item.Defindex == 5000)
                    MetalPutUp++;
                else if (item.Defindex == 5001)
                    MetalPutUp += 3;
                else if (item.Defindex == 5002)
                    MetalPutUp += 9;
                else
                {
                    var schemaItem = Trade.CurrentSchema.GetItem (item.Defindex);
                    errors.Add ("'" + schemaItem.Name + "' is not a ticket or metal.");
                }
            }
            
            if (reportEmpty && TicketsPutUp < 1 && MetalPutUp < 1) {
                errors.Add ("You must put up at least 1 ticket or some metal.");
            }

			if (TicketsPutUp > 0 && MetalPutUp > 0) {
				errors.Add ("You can only put up tickets OR metal, not both.");
			}
            
            // send the errors
            if (errors.Count > 0)
            {
                Trade.SendMessage("There were errors in your trade: ");
                foreach (string error in errors)
                {
                    Trade.SendMessage(error);
                }
            }
            else
            {
                MatchItems();
            }
            
			valid = (errors.Count == 0);

            return valid;
        }

        private void MatchItems()
        {
            if (MetalPutUp > 0)
            {
                int numTickets = MetalPutUp / MetalCostPerTicket;
                int spareMetal = MetalPutUp % MetalCostPerTicket;
                while (MyTicketsPutUp > numTickets)
                {
                    if (Trade.RemoveItemByDefindex(TICKET_ID))
                        MyTicketsPutUp--;
                    else break;
                }
                while (MyTicketsPutUp < numTickets)
                {
                    if (Trade.RemoveItemByDefindex(TICKET_ID))
                        MyTicketsPutUp++;
                    else
                    {
                        Trade.SendMessage("I do not have enough tickets, offering what I can!");
                        break;
                    }
                }
                if (spareMetal > 0)
                {
                    if (numTickets == 0)
                        Trade.SendMessage("You have not offered enough metal for a ticket, I will not offer anything in return!");
                    else
                    {
                        while (MyMetalPutUp > spareMetal)
                        {
                            if (Trade.RemoveItemByDefindex(5002))
                                MyMetalPutUp -= 9;
                            else if (Trade.RemoveItemByDefindex(5001))
                                MyMetalPutUp -= 3;
                            else if (Trade.RemoveItemByDefindex(5000))
                                MyMetalPutUp--;
                            else break;
                        }
                        while (MyMetalPutUp < spareMetal)
                        {
                            if ((spareMetal - 8) > MyMetalPutUp && Trade.AddItemByDefindex(5002))
                                MyMetalPutUp += 9;
                            else if ((spareMetal - 2) > MyMetalPutUp && Trade.AddItemByDefindex(5001))
                                MyMetalPutUp += 3;
                            else if (Trade.AddItemByDefindex(5000))
                                MyMetalPutUp++;
                            else break;
                        }
                        if(spareMetal > MyMetalPutUp)
                            Trade.SendMessage("You have left extra metal in your offer (" + FormatMetal(spareMetal) + "), and I don't have change!");
                    }
                }
            }
            else
            {
                MyTicketsPutUp = 0;
                Trade.RemoveAllItemsByDefindex(TICKET_ID);
                int numMetal = TicketsPutUp * MetalGivenPerTicket;
                while (numMetal < MyMetalPutUp)
                {
                    if (Trade.RemoveItemByDefindex(5000))
                        MyMetalPutUp--;
                    else if (Trade.RemoveItemByDefindex(5001))
                        MyMetalPutUp -= 3;
                    else if(Trade.RemoveItemByDefindex(5002))
                        MyMetalPutUp -= 9;
                    else break;
                }
                while (numMetal > MyMetalPutUp)
                {
                    if ((numMetal - 8) > MyMetalPutUp && Trade.AddItemByDefindex(5002))
                        MyMetalPutUp += 9;
                    else if ((numMetal - 2) > MyMetalPutUp && Trade.AddItemByDefindex(5001))
                        MyMetalPutUp += 3;
                    else if (Trade.AddItemByDefindex(5000))
                        MyMetalPutUp++;
                    else
                    {
                        Trade.SendMessage("I do not have enough metal or do not have change to match your ticket offer, offering what I can!");
                        break;
                    }
                }
            }
        }

        private string FormatMetal(int metal)
        {
            int refined = metal / 9;
            int remainder = metal % 9;
            return refined.ToString() + "." + remainder.ToString() + remainder.ToString() + "ref";
        }
    }
 
}

