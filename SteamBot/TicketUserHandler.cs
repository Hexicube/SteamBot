using SteamKit2;
using System.Collections.Generic;
using SteamTrade;

namespace SteamBot
{
    public class TicketUserHandler : UserHandler
    {
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
        
        public override void OnMessage (string message, EChatEntryType type) 
        {
            if (type != EChatEntryType.ChatMsg) return;
            if(message.ToLower().Equals("help"))
            {
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "To view my current prices, say \"prices\".");
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "To check how many items I have, say \"stock\".");
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "To start a trade, request one.");
            }
            else if(message.ToLower().Equals("price") || message.ToLower().Equals("prices"))
            {
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Current prices:");
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, FormatMetal(MetalCostPerTicket) + " to buy each ticket");
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, FormatMetal(MetalGivenPerTicket) + " offered for each ticket");
            }
            else if(message.ToLower().Equals("stock"))
            {
                Bot.GetInventory();
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Current stock:");
                int count1 = Bot.MyInventory.GetNumItemsByDefindex(TICKET_ID);
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, count1.ToString() + " ticket" + (count1==1?"":"s"));
                int count2 = Bot.MyInventory.GetNumItemsByDefindex(5000);
                int count3 = Bot.MyInventory.GetNumItemsByDefindex(5001);
                int count4 = Bot.MyInventory.GetNumItemsByDefindex(5002);
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, FormatMetal(count2+count3*3+count4*9) + " (" + count2.ToString() + " scrap, " + count3.ToString() + " reclaimed, " + count4.ToString() + " refined)");
            }
            else Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Hello, I am an automated trading bot. To get started, offer a trade! For a list of commands, type \"help\".");
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
            Bot.log.Info ("User was kicked because he was AFK.");
        }
        
        public override void OnTradeInit()
        {
            Trade.SendMessage("To trade, offer your metal/tickets and accept the trade, I will then add the appropriate metal/tickets for what you offered.");
            Trade.SendMessage("You can also type 'update' if you want me to check the trade and add my items.");
        }
        
        public override void OnTradeAddItem (Schema.Item schemaItem, Inventory.Item inventoryItem) {}
        
        public override void OnTradeRemoveItem (Schema.Item schemaItem, Inventory.Item inventoryItem) {}
        
        public override void OnTradeMessage (string message)
        {
            if (message.ToLower().Equals("update")) Validate();
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
                if(Validate ())
                {
                    if (MetalPutUp > 0)
                    {
                        int numTickets = MetalPutUp / MetalCostPerTicket;
                        int spareMetal = MetalPutUp % MetalCostPerTicket;
                        if (numTickets == 0)
                        {
                            Trade.SendMessage("You have not added enough metal for a ticket, I will not offer anything in return!");
                        }
                        else if (spareMetal > 0)
                        {
                            Trade.SendMessage("You have left extra metal in your offer (" + FormatMetal(spareMetal) + "), I will not give change!");
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
                //Even if it is successful, AcceptTrade can fail on
                //trades with a lot of items so we use a try-catch
                try {
                    Trade.AcceptTrade();
                }
                catch {
                    Log.Warn ("The trade might have failed, but we can't be sure.");
                }

                Log.Success ("Trade Complete!");
            }

            OnTradeClose ();
        }

        public bool Validate ()
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
                    errors.Add ("Item " + schemaItem.Name + " is not a ticket or metal.");
                }
            }
            
            if (TicketsPutUp < 1 && MetalPutUp < 1) {
                errors.Add ("You must put up at least 1 ticket or some metal.");
            }

			if (TicketsPutUp > 0 && MetalPutUp > 0) {
				errors.Add ("You can only put up tickets OR metal, not both.");
			}
            
            // send the errors
            if (errors.Count != 0)
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
                MyMetalPutUp = 0;
                Trade.RemoveAllItemsByDefindex(5000);
                Trade.RemoveAllItemsByDefindex(5001);
                Trade.RemoveAllItemsByDefindex(5002);
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
                        Trade.SendMessage("You have leftover metal in your offer (" + FormatMetal(spareMetal) + "), I will not offer change!");
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

