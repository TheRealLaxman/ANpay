using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class AiAssistantService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AiAssistantService> _logger;

    public AiAssistantService(ApplicationDbContext context, ILogger<AiAssistantService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AiChat> CreateChatAsync(string userId, string? title = null)
    {
        var chat = new AiChat
        {
            UserId = userId,
            SessionTitle = title ?? "New Chat"
        };

        _context.AiChats.Add(chat);
        await _context.SaveChangesAsync();
        return chat;
    }

    public async Task<List<AiChat>> GetUserChatsAsync(string userId)
    {
        return await _context.AiChats
            .Where(ac => ac.UserId == userId && ac.Status == AiChatStatus.Active)
            .OrderByDescending(ac => ac.LastMessageAt)
            .ToListAsync();
    }

    public async Task<AiChat?> GetChatByIdAsync(Guid chatId, string userId)
    {
        return await _context.AiChats
            .FirstOrDefaultAsync(ac => ac.Id == chatId && ac.UserId == userId);
    }

    public async Task<AiMessage> SendMessageAsync(Guid chatId, string userId, string content)
    {
        var chat = await _context.AiChats.FirstOrDefaultAsync(ac => ac.Id == chatId && ac.UserId == userId);
        if (chat == null) throw new NotFoundException("Chat session not found");

        var userMessage = new AiMessage
        {
            AiChatId = chatId,
            Role = AiMessageRole.User,
            Content = content
        };

        _context.AiMessages.Add(userMessage);

        // Get conversation history for context
        var recentMessages = await _context.AiMessages
            .Where(am => am.AiChatId == chatId)
            .OrderByDescending(am => am.CreatedAt)
            .Take(10)
            .ToListAsync();

        var response = await GenerateResponseAsync(userId, content, recentMessages);

        var assistantMessage = new AiMessage
        {
            AiChatId = chatId,
            Role = AiMessageRole.Assistant,
            Content = response.Content,
            Intent = response.Intent,
            EntityType = response.EntityType,
            EntityId = response.EntityId
        };

        _context.AiMessages.Add(assistantMessage);

        chat.LastMessageAt = DateTime.UtcNow;
        if (chat.SessionTitle == "New Chat")
        {
            chat.SessionTitle = content.Length > 50 ? content[..50] + "..." : content;
        }

        await _context.SaveChangesAsync();
        return assistantMessage;
    }

    public async Task<List<AiMessage>> GetChatMessagesAsync(Guid chatId, string userId)
    {
        var chat = await _context.AiChats.FirstOrDefaultAsync(ac => ac.Id == chatId && ac.UserId == userId);
        if (chat == null) throw new NotFoundException("Chat session not found");

        return await _context.AiMessages
            .Where(am => am.AiChatId == chatId)
            .OrderBy(am => am.CreatedAt)
            .ToListAsync();
    }

    public async Task<AiChat> ArchiveChatAsync(Guid chatId, string userId)
    {
        var chat = await _context.AiChats.FirstOrDefaultAsync(ac => ac.Id == chatId && ac.UserId == userId);
        if (chat == null) throw new NotFoundException("Chat session not found");

        chat.Status = AiChatStatus.Archived;
        await _context.SaveChangesAsync();
        return chat;
    }

    private async Task<AiResponse> GenerateResponseAsync(string userId, string userMessage, List<AiMessage> conversationHistory)
    {
        var lowerMessage = userMessage.ToLower().Trim();
        var user = await _context.Users.FindAsync(userId);
        var role = user?.Role ?? AppUserRole.Customer;

        // Extract intent and entities using keyword analysis
        var intent = DetectIntent(lowerMessage);

        // Balance inquiry
        if (intent == "balance" || lowerMessage.Contains("balance") || lowerMessage.Contains("how much") || lowerMessage.Contains("wallet balance"))
        {
            var wallets = await _context.Wallets.Where(w => w.UserId == userId && w.IsActive).ToListAsync();
            var totalBalance = wallets.Sum(w => w.Balance);

            if (wallets.Count == 0)
            {
                return new AiResponse
                {
                    Content = "You don't have any wallets yet. Would you like me to help you create one?",
                    Intent = "check_balance",
                    EntityType = "wallet"
                };
            }

            var walletDetails = string.Join("\n", wallets.Select(w =>
                $"  \u2022 {w.WalletName}: {w.Currency} {w.Balance:N2}"));

            var response = $"Your total balance across all wallets: **{totalBalance:N2}**\n\nWallet details:\n{walletDetails}";

            // Add helpful suggestions based on balance
            if (totalBalance == 0)
            {
                response += "\n\nYour wallets are empty. You can deposit funds via bank transfer, card, or visit a branch.";
            }
            else if (totalBalance < 100)
            {
                response += "\n\nYour balance is running low. Consider topping up to continue transacting.";
            }

            return new AiResponse
            {
                Content = response,
                Intent = "check_balance",
                EntityType = "wallet"
            };
        }

        // Transaction history
        if (intent == "transactions" || lowerMessage.Contains("transaction") || lowerMessage.Contains("history") || lowerMessage.Contains("recent") || lowerMessage.Contains("last"))
        {
            var recentTx = await _context.Transactions
                .Where(t => t.Wallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            if (recentTx.Any())
            {
                var txList = string.Join("\n", recentTx.Select(t =>
                {
                    var emoji = t.Type switch
                    {
                        TransactionType.Deposit => "\ud83d\udfe5",
                        TransactionType.Withdrawal => "\ud83d\udfe7",
                        TransactionType.TransferIn => "\ud83d\udfe8",
                        TransactionType.TransferOut => "\ud83d\udfe9",
                        TransactionType.Payment => "\ud83d\udfe6",
                        _ => "\u26aa"
                    };
                    return $"  {emoji} {t.CreatedAt:MMM dd}: {t.Type} - {t.Amount:N2} ({t.Status})";
                }));

                return new AiResponse
                {
                    Content = $"Here are your last 5 transactions:\n\n{txList}\n\nWould you like to see more details or filter by date?",
                    Intent = "check_transactions",
                    EntityType = "transaction"
                };
            }
            return new AiResponse
            {
                Content = "You don't have any transactions yet. Start by making a deposit or transfer!",
                Intent = "check_transactions",
                EntityType = "transaction"
            };
        }

        // Transfer help
        if (intent == "transfer" || lowerMessage.Contains("transfer") || lowerMessage.Contains("send money"))
        {
            return new AiResponse
            {
                Content = "**How to transfer money:**\n\n1. Go to your Wallet\n2. Click 'Transfer'\n3. Enter the destination wallet ID or select a beneficiary\n4. Enter the amount\n5. Confirm with your transaction PIN\n\nYou can also:\n- Save beneficiaries for quick transfers\n- Set up scheduled/recurring transfers\n- Use QR codes for instant payments\n\nNeed help with a specific transfer?",
                Intent = "transfer_help",
                EntityType = "wallet"
            };
        }

        // Bill payment help
        if (intent == "bill_payment" || lowerMessage.Contains("bill") || lowerMessage.Contains("pay") || lowerMessage.Contains("electricity") || lowerMessage.Contains("airtime") || lowerMessage.Contains("data"))
        {
            return new AiResponse
            {
                Content = "**Bill Payment Services:**\n\n\u2022 **Electricity**: IKEDC, PHED, and more\n\u2022 **Airtime & Data**: MTN, Airtel, Glo, 9Mobile\n\u2022 **Cable TV**: DStv, GOtv, Startimes\n\u2022 **Internet**: Various ISPs\n\u2022 **Water**: State water boards\n\n**Steps:**\n1. Go to 'Bill Payments'\n2. Select category and provider\n3. Enter meter/phone number\n4. Enter amount\n5. Confirm payment\n\nWhich bill would you like to pay?",
                Intent = "bill_payment_help",
                EntityType = "bill"
            };
        }

        // Virtual card help
        if (intent == "virtual_card" || lowerMessage.Contains("card") || lowerMessage.Contains("virtual"))
        {
            return new AiResponse
            {
                Content = "**Virtual Cards** let you shop online safely!\n\n**Features:**\n\u2022 Create instantly linked to your wallet\n\u2022 Set daily/monthly spending limits\n\u2022 Freeze/unfreeze anytime\n\u2022 Use for online shopping globally\n\u2022 Disposable cards for one-time purchases\n\n**Card Types:**\n- Standard: Basic online payments\n- Premium: Lower fees, higher limits\n- Business: Multi-user, expense tracking\n- Disposable: One-time use, enhanced privacy\n\nGo to 'Virtual Cards' to create one. Need help with specific card features?",
                Intent = "card_help",
                EntityType = "virtual_card"
            };
        }

        // Loan help
        if (intent == "loan" || lowerMessage.Contains("loan") || lowerMessage.Contains("borrow") || lowerMessage.Contains("credit"))
        {
            var creditScore = await _context.CreditScores.FirstOrDefaultAsync(cs => cs.UserId == userId);
            var response = "**Microloans** based on your wallet activity!\n\n**Features:**\n\u2022 Loans from 5,000 to 500,000\n\u2022 Flexible repayment terms (7-90 days)\n\u2022 Competitive interest rates\n\u2022 Auto-debit from your wallet\n\u2022 No collateral required\n\n";

            if (creditScore != null)
            {
                response += $"**Your Credit Score:** {creditScore.Score} ({creditScore.Rating})\n";
                response += $"**Available Credit:** {creditScore.MaximumCreditLimit:N2}\n\n";

                if (creditScore.Score >= 670)
                {
                    response += "You're eligible for a loan! Would you like to apply?";
                }
                else
                {
                    response += "Improve your score by making more transactions and completing KYC.";
                }
            }
            else
            {
                response += "Your credit score is being calculated based on your wallet activity.\n\nCheck your 'Credit Score' section for updates.";
            }

            return new AiResponse
            {
                Content = response,
                Intent = "loan_help",
                EntityType = "microloan"
            };
        }

        // Investment help
        if (intent == "investment" || lowerMessage.Contains("invest") || lowerMessage.Contains("savings") || lowerMessage.Contains("goal") || lowerMessage.Contains("interest"))
        {
            var investments = await _context.Investments
                .Where(i => i.UserId == userId && i.Status == InvestmentStatus.Active)
                .ToListAsync();

            var savingsGoals = await _context.SavingsGoals
                .Where(sg => sg.UserId == userId && sg.Status == SavingsGoalStatus.Active)
                .ToListAsync();

            var response = "**Grow your money with ANpay:**\n\n**Investment Options:**\n\u2022 Fixed Deposits (up to 15% p.a.)\n\u2022 Treasury Bills\n\u2022 Money Market Funds\n\u2022 Mutual Funds\n\n**Savings Goals:**\n\u2022 Set targets and track progress\n\u2022 Auto-save on schedule\n\u2022 Earn interest on savings\n\n";

            if (investments.Any())
            {
                response += $"**Your Active Investments:** {investments.Count}\n";
                response += $"Total Invested: {investments.Sum(i => i.PrincipalAmount):N2}\n";
                response += $"Interest Earned: {investments.Sum(i => i.InterestEarned):N2}\n\n";
            }

            if (savingsGoals.Any())
            {
                response += $"**Your Savings Goals:** {savingsGoals.Count}\n";
                foreach (var goal in savingsGoals.Take(3))
                {
                    var progress = goal.TargetAmount > 0 ? (goal.CurrentAmount / goal.TargetAmount * 100) : 0;
                    response += $"- {goal.GoalName}: {progress:F0}% ({goal.CurrentAmount:N2}/{goal.TargetAmount:N2})\n";
                }
            }

            response += "\nWhich option interests you?";
            return new AiResponse
            {
                Content = response,
                Intent = "investment_help",
                EntityType = "investment"
            };
        }

        // Remittance help
        if (intent == "remittance" || (lowerMessage.Contains("send") && (lowerMessage.Contains("abroad") || lowerMessage.Contains("international") || lowerMessage.Contains("remittance"))))
        {
            var countries = await _context.RemittancePartners
                .Where(rp => rp.IsActive)
                .Select(rp => rp.Country)
                .Distinct()
                .Take(10)
                .ToListAsync();

            var response = "**Send money internationally with ANpay!**\n\n";
            response += "**Supported Countries:**\n";
            foreach (var country in countries)
            {
                response += $"- {country}\n";
            }

            response += "\n**Features:**\n";
            response += "\u2022 Competitive exchange rates\n";
            response += "\u2022 Fast delivery (1-2 business days)\n";
            response += "\u2022 Track your transfer in real-time\n";
            response += "\u2022 Low fees\n\n";

            response += "Which country are you sending to?";

            return new AiResponse
            {
                Content = response,
                Intent = "remittance_help",
                EntityType = "remittance"
            };
        }

        // Credit score
        if (intent == "credit_score" || lowerMessage.Contains("credit score") || lowerMessage.Contains("my score"))
        {
            var creditScore = await _context.CreditScores.FirstOrDefaultAsync(cs => cs.UserId == userId);
            if (creditScore != null)
            {
                var factors = await _context.CreditScoreFactors
                    .Where(csf => csf.CreditScoreId == creditScore.Id)
                    .OrderByDescending(csf => Math.Abs(csf.Impact))
                    .Take(5)
                    .ToListAsync();

                var response = $"**Your Credit Score:** {creditScore.Score} ({creditScore.Rating})\n\n";
                response += $"**Credit Limit:** {creditScore.MaximumCreditLimit:N2}\n";
                response += $"**Interest Rate:** {creditScore.InterestRate}%\n\n";

                if (factors.Any())
                {
                    response += "**Key Factors:**\n";
                    foreach (var factor in factors)
                    {
                        var impact = factor.Impact >= 0 ? $"+{factor.Impact}" : factor.Impact.ToString();
                        response += $"- {factor.FactorName}: {impact} ({factor.Description})\n";
                    }
                }

                response += "\n**To improve your score:**\n";
                response += "- Make regular transactions\n";
                response += "- Complete your KYC verification\n";
                response += "- Pay loans on time\n";
                response += "- Maintain good account standing";

                return new AiResponse
                {
                    Content = response,
                    Intent = "check_credit_score",
                    EntityType = "credit_score"
                };
            }
            return new AiResponse
            {
                Content = "Your credit score is being calculated based on your wallet activity. Keep using ANpay to build your score!",
                Intent = "check_credit_score",
                EntityType = "credit_score"
            };
        }

        // Loyalty & rewards
        if (intent == "loyalty" || lowerMessage.Contains("loyalty") || lowerMessage.Contains("points") || lowerMessage.Contains("reward") || lowerMessage.Contains("cashback"))
        {
            var loyalty = await _context.LoyaltyPoints.FirstOrDefaultAsync(lp => lp.UserId == userId);
            if (loyalty != null)
            {
                var response = $"**Your Loyalty Status:** {loyalty.Tier}\n\n";
                response += $"**Available Points:** {loyalty.AvailablePoints}\n";
                response += $"**Lifetime Points:** {loyalty.LifetimePoints}\n";
                response += $"**Points Used:** {loyalty.UsedPoints}\n\n";

                response += "**How to earn points:**\n";
                response += "- Every transaction earns points\n";
                response += "- Refer friends for bonus points\n";
                response += "- Special promotions for bonus rewards\n\n";

                response += "**Redeem for:**\n";
                response += "- Airtime & data\n";
                response += "- Cashback to wallet\n";
                response += "- Gift cards\n";
                response += "- Bill payments";

                return new AiResponse
                {
                    Content = response,
                    Intent = "check_loyalty",
                    EntityType = "loyalty"
                };
            }
            return new AiResponse
            {
                Content = "Start earning loyalty points on every transaction! Points can be redeemed for airtime, gift cards, or cashback. Your points accumulate automatically.",
                Intent = "check_loyalty",
                EntityType = "loyalty"
            };
        }

        // Insurance
        if (intent == "insurance" || lowerMessage.Contains("insurance") || lowerMessage.Contains("cover") || lowerMessage.Contains("policy"))
        {
            return new AiResponse
            {
                Content = "**Insurance Products:**\n\n\u2022 **Health Insurance**: Comprehensive medical coverage\n\u2022 **Life Insurance**: Protect your family's future\n\u2022 **Vehicle Insurance**: Car and motorcycle coverage\n\u2022 **Travel Insurance**: Coverage for trips abroad\n\u2022 **Device Insurance**: Protect your gadgets\n\u2022 **Property Insurance**: Home and property coverage\n\n**Benefits:**\n- Pay premiums from your wallet\n- File claims in-app\n- Fast claim processing\n- Flexible payment frequencies\n\nGo to 'Insurance' to explore plans. Which type interests you?",
                Intent = "insurance_help",
                EntityType = "insurance"
            };
        }

        // Disputes
        if (intent == "dispute" || lowerMessage.Contains("dispute") || lowerMessage.Contains("refund") || lowerMessage.Contains("complaint"))
        {
            return new AiResponse
            {
                Content = "**File a Dispute:**\n\nIf you have an issue with a transaction:\n1. Go to 'Disputes' in the menu\n2. Click 'New Dispute'\n3. Select the transaction\n4. Describe the issue\n5. Submit\n\n**What happens next:**\n- Our team reviews within 24-48 hours\n- You'll receive updates via email/SMS\n- Refund processed if dispute is valid\n\n**Tips:**\n- Include all relevant details\n- Attach screenshots if possible\n- Reference the transaction number\n\nWould you like to file a dispute now?",
                Intent = "dispute_help",
                EntityType = "dispute"
            };
        }

        // Support/help
        if (intent == "support" || lowerMessage.Contains("help") || lowerMessage.Contains("support") || lowerMessage.Contains("ticket"))
        {
            return new AiResponse
            {
                Content = "**I can help you with:**\n\n\u2022 **Balance & Transactions**: Check wallet activity\n\u2022 **Transfers & Payments**: Send money, pay bills\n\u2022 **Virtual Cards**: Create, manage, freeze cards\n\u2022 **Investments**: Grow your money\n\u2022 **Credit Score**: Check and improve your score\n\u2022 **Loyalty**: Earn and redeem points\n\u2022 **Insurance**: Protect what matters\n\u2022 **Disputes**: Report issues\n\u2022 **Remittance**: Send money abroad\n\n**For complex issues:**\n- Create a support ticket\n- Our team responds within 24 hours\n\nWhat would you like to know about?",
                Intent = "general_help",
                EntityType = null
            };
        }

        // Greeting
        if (intent == "greeting" || lowerMessage.Contains("hello") || lowerMessage.Contains("hi") || lowerMessage.Contains("hey") || lowerMessage.Contains("good morning") || lowerMessage.Contains("good afternoon"))
        {
            var timeOfDay = DateTime.UtcNow.Hour switch
            {
                < 12 => "Good morning",
                < 17 => "Good afternoon",
                _ => "Good evening"
            };

            return new AiResponse
            {
                Content = $"{timeOfDay}{($", {user?.FirstName}")}! 👋\n\nI'm your ANpay assistant. I can help you with:\n\n- Checking balances & transactions\n- Making transfers & payments\n- Virtual cards & investments\n- Credit scores & loyalty rewards\n- Insurance & remittances\n- Disputes & support\n\nWhat would you like to do today?",
                Intent = "greeting",
                EntityType = null
            };
        }

        // Thanks
        if (intent == "thanks" || lowerMessage.Contains("thank") || lowerMessage.Contains("thanks"))
        {
            return new AiResponse
            {
                Content = "You're welcome! Is there anything else I can help you with? 😊",
                Intent = "thanks",
                EntityType = null
            };
        }

        // Goodbye
        if (lowerMessage.Contains("bye") || lowerMessage.Contains("goodbye") || lowerMessage.Contains("see you"))
        {
            return new AiResponse
            {
                Content = "Goodbye! Have a great day. Feel free to come back anytime you need help. 👋",
                Intent = "goodbye",
                EntityType = null
            };
        }

        // Admin/Official specific responses
        if (role == AppUserRole.SuperAdmin || role == AppUserRole.BranchAdmin)
        {
            if (intent == "dashboard" || lowerMessage.Contains("dashboard") || lowerMessage.Contains("stats") || lowerMessage.Contains("report"))
            {
                var totalUsers = await _context.Users.CountAsync();
                var totalWallets = await _context.Wallets.CountAsync();
                var todayTransactions = await _context.Transactions
                    .CountAsync(t => t.CreatedAt.Date == DateTime.UtcNow.Date);
                var todayVolume = await _context.Transactions
                    .Where(t => t.CreatedAt.Date == DateTime.UtcNow.Date && t.Status == TransactionStatus.Completed)
                    .SumAsync(t => t.Amount);

                return new AiResponse
                {
                    Content = $"**Today's Dashboard:**\n\n- Total Users: {totalUsers}\n- Total Wallets: {totalWallets}\n- Today's Transactions: {todayTransactions}\n- Today's Volume: {todayVolume:N2}\n\nGo to the Admin Dashboard for detailed reports and analytics.",
                    Intent = "admin_dashboard",
                    EntityType = "report"
                };
            }

            if (intent == "fraud" || lowerMessage.Contains("fraud") || lowerMessage.Contains("suspicious"))
            {
                var pendingAlerts = await _context.FraudAlerts.CountAsync(fa => fa.Status == FraudAlertStatus.Open);
                var highRisk = await _context.FraudAlerts.CountAsync(fa => fa.Status == FraudAlertStatus.Open && fa.RiskScore > 70);

                return new AiResponse
                {
                    Content = $"**Fraud Alert Summary:**\n\n- Pending Alerts: {pendingAlerts}\n- High Risk: {highRisk}\n\nGo to 'Fraud Detection' to review and resolve alerts.",
                    Intent = "fraud_check",
                    EntityType = "fraud_alert"
                };
            }
        }

        // Check training data for matching responses
        var trainingMatch = await FindBestTrainingMatch(lowerMessage);
        if (trainingMatch != null)
        {
            return new AiResponse
            {
                Content = trainingMatch.Answer,
                Intent = "training_data",
                EntityType = null
            };
        }

        // Default response with suggestions
        return new AiResponse
        {
            Content = "I'm not sure I understand. Could you rephrase that?\n\nHere are some things I can help with:\n- **Check balance** - See your wallet balance\n- **Transactions** - View recent activity\n- **Transfer** - Send money\n- **Pay bills** - Electricity, airtime, etc.\n- **Virtual cards** - Create/manage cards\n- **Investments** - Grow your money\n- **Credit score** - Check your score\n- **Loyalty** - Points and rewards\n- **Support** - Get help\n\nJust ask me anything!",
            Intent = "unknown",
            EntityType = null
        };
    }

    private static string DetectIntent(string message)
    {
        // Intent detection based on keywords
        if (message.Contains("balance") || message.Contains("how much") || message.Contains("wallet")) return "balance";
        if (message.Contains("transaction") || message.Contains("history") || message.Contains("recent")) return "transactions";
        if (message.Contains("transfer") || message.Contains("send money")) return "transfer";
        if (message.Contains("bill") || message.Contains("pay") || message.Contains("electricity") || message.Contains("airtime")) return "bill_payment";
        if (message.Contains("card") || message.Contains("virtual")) return "virtual_card";
        if (message.Contains("loan") || message.Contains("borrow")) return "loan";
        if (message.Contains("invest") || message.Contains("savings") || message.Contains("goal")) return "investment";
        if (message.Contains("send") && (message.Contains("abroad") || message.Contains("international"))) return "remittance";
        if (message.Contains("credit score") || message.Contains("credit")) return "credit_score";
        if (message.Contains("loyalty") || message.Contains("points") || message.Contains("reward")) return "loyalty";
        if (message.Contains("insurance") || message.Contains("cover")) return "insurance";
        if (message.Contains("dispute") || message.Contains("refund")) return "dispute";
        if (message.Contains("help") || message.Contains("support")) return "support";
        if (message.Contains("hello") || message.Contains("hi") || message.Contains("hey")) return "greeting";
        if (message.Contains("thank")) return "thanks";
        if (message.Contains("bye") || message.Contains("goodbye")) return "goodbye";
        if (message.Contains("dashboard") || message.Contains("stats")) return "dashboard";
        if (message.Contains("fraud") || message.Contains("suspicious")) return "fraud";

        return "unknown";
    }

    private async Task<AiTrainingData?> FindBestTrainingMatch(string message)
    {
        var trainingData = await _context.AiTrainingData.ToListAsync();

        AiTrainingData? bestMatch = null;
        int bestScore = 0;

        foreach (var data in trainingData)
        {
            var keywords = data.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries);
            int score = 0;

            foreach (var keyword in keywords)
            {
                if (message.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    score++;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = data;
            }
        }

        return bestScore >= 2 ? bestMatch : null;
    }

    public async Task SeedTrainingDataAsync()
    {
        if (await _context.AiTrainingData.AnyAsync()) return;

        var trainingData = new List<AiTrainingData>
        {
            new() { Category = "Getting Started", Question = "How do I create a wallet?", Answer = "Go to Wallets > Create Wallet. Choose a name and currency for your new wallet.", Keywords = "wallet,create,new", MinRole = 0 },
            new() { Category = "Getting Started", Question = "How do I add money?", Answer = "Go to your wallet and click Deposit. You can add funds via bank transfer, card, or cash deposit at a branch.", Keywords = "deposit,money,add,fund", MinRole = 0 },
            new() { Category = "Transfers", Question = "How do I send money?", Answer = "Go to Wallet > Transfer. Enter the destination wallet ID, amount, and confirm with your PIN.", Keywords = "transfer,send,money,pay", MinRole = 0 },
            new() { Category = "Security", Question = "How do I change my password?", Answer = "Go to Profile > Security > Change Password. Enter your current and new password.", Keywords = "password,change,security", MinRole = 0 },
            new() { Category = "Security", Question = "How do I set up 2FA?", Answer = "Go to Profile > Security > Two-Factor Authentication. Follow the setup wizard.", Keywords = "2fa,two-factor,authentication,security", MinRole = 0 },
            new() { Category = "Cards", Question = "How do I create a virtual card?", Answer = "Go to Virtual Cards > Create Card. Choose card type and set spending limits.", Keywords = "virtual,card,create", MinRole = 0 },
            new() { Category = "Bills", Question = "How do I pay electricity bills?", Answer = "Go to Bill Payments > Electricity. Select your provider, enter meter number and amount.", Keywords = "electricity,bill,pay,meter", MinRole = 0 },
            new() { Category = "Bills", Question = "How do I buy airtime?", Answer = "Go to Bill Payments > Airtime. Select your network provider, enter phone number and amount.", Keywords = "airtime,phone,topup,recharge", MinRole = 0 },
            new() { Category = "Investments", Question = "How do I invest my money?", Answer = "Go to Investments > Create Investment. Choose product type, amount and tenure.", Keywords = "invest,money,grow,interest", MinRole = 0 },
            new() { Category = "Loans", Question = "How do I get a loan?", Answer = "Go to Microloans > Apply. Your credit score determines eligibility and interest rate.", Keywords = "loan,borrow,micro,credit", MinRole = 0 },
            new() { Category = "Support", Question = "How do I contact support?", Answer = "Go to Support > Create Ticket. Describe your issue and our team will respond within 24 hours.", Keywords = "support,ticket,contact,help", MinRole = 0 },
        };

        _context.AiTrainingData.AddRange(trainingData);
        await _context.SaveChangesAsync();
    }
}

public class AiResponse
{
    public string Content { get; set; } = string.Empty;
    public string? Intent { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
}
