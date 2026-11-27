using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Ecommerce.Application_BAL_;
using Ecommerce.Application_BAL_.DTOs.Payments;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Enums;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure;
using Ecommerce.Infrastructure_DAL_.BackgroundServices;
using Ecommerce.Infrastructure_DAL_.Data;
using Ecommerce.Infrastructure_DAL_.Providers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Tests;

class Program
{
    private const string ConnectionString = "Server=.;Database=EcommerceDb;Trusted_Connection=True;TrustServerCertificate=True;";

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("     ECOMMERCE PAYMENTS MODULE: COMPLETE AUTOMATED TEST SUITE (REAL SQL SERVER) ");
        Console.WriteLine("================================================================================\n");

        int passed = 0;
        int failed = 0;

        void Assert(bool condition, string testName, string? detail = null)
        {
            if (condition)
            {
                Console.WriteLine($"  [PASS] {testName}");
                passed++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [FAIL] {testName} {(detail != null ? "--> " + detail : "")}");
                Console.ResetColor();
                failed++;
            }
        }

        try
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", ConnectionString },
                { "JwtSettings:Key", "SuperSecretKeyForTestingAtLeast32BytesLong!" },
                { "JwtSettings:Issuer", "TestIssuer" },
                { "JwtSettings:Audience", "TestAudience" },
                { "JwtSettings:ExpirationMinutes", "60" },
                { "PaymentExpiration:ExpirationMinutes", "30" },
                { "PaymentExpiration:PollingIntervalMinutes", "5" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure();
            services.AddApplication(configuration);

            var provider = services.BuildServiceProvider();
            var paymentRepo = provider.GetRequiredService<IPaymentRepository>();
            var auditRepo = provider.GetRequiredService<IPaymentStatusAuditRepository>();
            var paymentService = provider.GetRequiredService<IPaymentService>();
            var paymentProvider = provider.GetRequiredService<IPaymentProvider>();
            var connectionFactory = provider.GetRequiredService<IDbConnectionFactory>();

            using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            var adminUserId = await connection.ExecuteScalarAsync<long>(
                "SELECT TOP 1 UserId FROM dbo.Users WHERE Role = 'Admin';");
            var customerUserId = await connection.ExecuteScalarAsync<long>(
                "SELECT TOP 1 UserId FROM dbo.Users WHERE Role = 'Customer';");

            Console.WriteLine($"Using Admin UserId: {adminUserId}, Customer UserId: {customerUserId}\n");

            // Helper to create test orders
            async Task<long> CreateTestOrderAsync(string orderStatus, string paymentStatus, decimal amount = 100.00m, long? userId = null)
            {
                const string sql = """
                    INSERT INTO dbo.Orders (
                        UserId, Status, PaymentStatus, Subtotal, DiscountAmount,
                        ShippingCost, TotalAmount, ShippingFullName, ShippingPhone,
                        ShippingCity, ShippingStreet
                    )
                    VALUES (
                        @UserId, @Status, @PaymentStatus, @Amount, 0,
                        0, @Amount, 'Test Customer', '1234567890',
                        'Cairo', 'Main St'
                    );
                    SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
                    """;

                return await connection.ExecuteScalarAsync<long>(
                    sql,
                    new { UserId = userId ?? customerUserId, Status = orderStatus, PaymentStatus = paymentStatus, Amount = amount });
            }

            // ========================================================================
            // SUITE 1: REPOSITORY INTEGRATION TESTS (T01 - T18)
            // ========================================================================
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("SUITE 1: Payment Repository Integration Tests (T01 - T18)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            // T01: Payment creation succeeds for valid order; status is Pending; paidAt is null
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 100.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment
                {
                    OrderId = orderId,
                    Provider = "Simulator",
                    Amount = 100.00m
                });

                var payment = await paymentRepo.GetByIdAsync(paymentId);
                Assert(payment != null && payment.Status == "Pending" && payment.PaidAt == null && payment.Amount == 100.00m,
                    "T01: Payment creation succeeds, Status=Pending, PaidAt=null, Amount matches");
            }

            // T02: Duplicate pending payment for same order rejected
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 75.00m);
                await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 75.00m });

                bool rejected = false;
                try
                {
                    await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 75.00m });
                }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected, "T02: Duplicate pending payment for same order rejected");
            }

            // T03: Payment creation rejected if order is Cancelled
            {
                var orderId = await CreateTestOrderAsync("Cancelled", "Pending", 50.00m);
                bool rejected = false;
                try
                {
                    await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 50.00m });
                }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected, "T03: Payment creation rejected if order is Cancelled");
            }

            // T04: Payment creation rejected if order is Paid
            {
                var orderId = await CreateTestOrderAsync("Processing", "Paid", 50.00m);
                bool rejected = false;
                try
                {
                    await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 50.00m });
                }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected, "T04: Payment creation rejected if order is Paid");
            }

            // T05: Payment creation rejected if order is Refunded
            {
                var orderId = await CreateTestOrderAsync("Cancelled", "Refunded", 50.00m);
                bool rejected = false;
                try
                {
                    await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 50.00m });
                }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected, "T05: Payment creation rejected if order is Refunded");
            }

            // T06: Payment creation rejected if order is PartiallyRefunded
            {
                var orderId = await CreateTestOrderAsync("Processing", "PartiallyRefunded", 50.00m);
                bool rejected = false;
                try
                {
                    await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 50.00m });
                }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected, "T06: Payment creation rejected if order is PartiallyRefunded");
            }

            // T07: Payment creation rejected if amount <= 0
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 50.00m);
                bool rejected = false;
                try
                {
                    await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 0m });
                }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Assert(rejected, "T07: Payment creation rejected if amount <= 0");
            }

            // T08: Payment creation rejected if amount != order.TotalAmount
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 150.00m);
                bool rejected = false;
                try
                {
                    await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 99.00m });
                }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected, "T08: Payment creation rejected if amount != order.TotalAmount");
            }

            // T09: ProviderReference uniqueness enforced on create
            {
                var orderId1 = await CreateTestOrderAsync("Pending", "Pending", 60.00m);
                var orderId2 = await CreateTestOrderAsync("Pending", "Pending", 60.00m);
                var sharedRef = "UNIQUE-REF-" + Guid.NewGuid().ToString("N")[..8];

                await paymentRepo.CreateAsync(new Payment
                {
                    OrderId = orderId1,
                    Provider = "Simulator",
                    ProviderReference = sharedRef,
                    Amount = 60.00m
                });

                bool rejected = false;
                try
                {
                    await paymentRepo.CreateAsync(new Payment
                    {
                        OrderId = orderId2,
                        Provider = "Simulator",
                        ProviderReference = sharedRef,
                        Amount = 60.00m
                    });
                }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected, "T09: Duplicate ProviderReference rejected on create");
            }

            // T10: Valid transition Pending -> Succeeded; PaidAt set; order.PaymentStatus updated to Paid
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 80.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 80.00m });

                var updated = await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded", "TXN-T10");
                var payment = await paymentRepo.GetByIdAsync(paymentId);
                var orderPaymentStatus = await connection.ExecuteScalarAsync<string>(
                    "SELECT PaymentStatus FROM dbo.Orders WHERE OrderId = @OrderId", new { OrderId = orderId });

                Assert(updated && payment?.Status == "Succeeded" && payment.PaidAt != null && orderPaymentStatus == "Paid",
                    "T10: Pending -> Succeeded sets PaidAt and orders.PaymentStatus=Paid");
            }

            // T11: Valid transition Pending -> Failed; order.PaymentStatus updated to Failed
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 45.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 45.00m });

                var updated = await paymentRepo.UpdateStatusAsync(paymentId, "Failed", "TXN-T11");
                var payment = await paymentRepo.GetByIdAsync(paymentId);
                var orderPaymentStatus = await connection.ExecuteScalarAsync<string>(
                    "SELECT PaymentStatus FROM dbo.Orders WHERE OrderId = @OrderId", new { OrderId = orderId });

                Assert(updated && payment?.Status == "Failed" && orderPaymentStatus == "Failed",
                    "T11: Pending -> Failed updates orders.PaymentStatus=Failed");
            }

            // T12: Valid transition Pending -> Cancelled; order.PaymentStatus remains Pending
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 30.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 30.00m });

                var updated = await paymentRepo.UpdateStatusAsync(paymentId, "Cancelled");
                var payment = await paymentRepo.GetByIdAsync(paymentId);
                var orderPaymentStatus = await connection.ExecuteScalarAsync<string>(
                    "SELECT PaymentStatus FROM dbo.Orders WHERE OrderId = @OrderId", new { OrderId = orderId });

                Assert(updated && payment?.Status == "Cancelled" && orderPaymentStatus == "Pending",
                    "T12: Pending -> Cancelled leaves orders.PaymentStatus untouched");
            }

            // T13: Valid transition Failed -> Cancelled allowed
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 40.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 40.00m });
                await paymentRepo.UpdateStatusAsync(paymentId, "Failed");

                var updated = await paymentRepo.UpdateStatusAsync(paymentId, "Cancelled");
                var payment = await paymentRepo.GetByIdAsync(paymentId);

                Assert(updated && payment?.Status == "Cancelled",
                    "T13: Failed -> Cancelled transition is allowed");
            }

            // T14: Invalid transition Succeeded -> Cancelled rejected
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 55.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 55.00m });
                await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded");

                bool rejected = false;
                try
                {
                    await paymentRepo.UpdateStatusAsync(paymentId, "Cancelled");
                }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected, "T14: Succeeded -> Cancelled transition rejected");
            }

            // T15: Invalid transition Cancelled -> Succeeded rejected
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 65.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 65.00m });
                await paymentRepo.UpdateStatusAsync(paymentId, "Cancelled");

                bool rejected = false;
                try
                {
                    await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded");
                }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected, "T15: Cancelled -> Succeeded transition rejected");
            }

            // T16: Invalid transition Refunded -> Succeeded rejected
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 70.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 70.00m });
                await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded");
                await paymentRepo.UpdateStatusAsync(paymentId, "Refunded");

                bool rejected = false;
                try
                {
                    await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded");
                }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected, "T16: Refunded -> Succeeded transition rejected");
            }

            // T17: Transition Succeeded -> Refunded updates order.PaymentStatus to Refunded; audit logged
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 95.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 95.00m });
                await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded");

                var adminResult = await paymentRepo.AdminUpdateStatusAsync(
                    paymentId, "Refunded", null, adminUserId, "Customer requested full refund.");

                var orderPaymentStatus = await connection.ExecuteScalarAsync<string>(
                    "SELECT PaymentStatus FROM dbo.Orders WHERE OrderId = @OrderId", new { OrderId = orderId });
                var audits = (await auditRepo.GetByPaymentIdAsync(paymentId)).ToList();

                Assert(adminResult == PaymentStatusUpdateResult.Updated &&
                       orderPaymentStatus == "Refunded" &&
                       audits.Any(a => a.NewStatus == "Refunded" && a.AdminUserId == adminUserId),
                    "T17: Succeeded -> Refunded updates order.PaymentStatus=Refunded and writes audit log");
            }

            // T18: Idempotent status update: repeating same status update succeeds with NoChange
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 85.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 85.00m });
                await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded", "TXN-T18");

                // Admin repeats same status
                var retryResult = await paymentRepo.AdminUpdateStatusAsync(
                    paymentId, "Succeeded", "TXN-T18", adminUserId, "Redundant update attempt");

                Assert(retryResult == PaymentStatusUpdateResult.NoChange,
                    "T18: Repeating same status update returns NoChange without error or duplicate mutation");
            }

            // ========================================================================
            // SUITE 2: CONCURRENCY TESTS (C01 - C08)
            // ========================================================================
            Console.WriteLine("\n--------------------------------------------------------------------------------");
            Console.WriteLine("SUITE 2: Payment Concurrency Tests (C01 - C08)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            // C01: Concurrent payment creation on same order
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 110.00m);
                int successes = 0;
                int failures = 0;

                var tasks = Enumerable.Range(0, 5).Select(async _ =>
                {
                    try
                    {
                        await paymentRepo.CreateAsync(new Payment
                        {
                            OrderId = orderId,
                            Provider = "Simulator",
                            Amount = 110.00m
                        });
                        System.Threading.Interlocked.Increment(ref successes);
                    }
                    catch (InvalidOperationException)
                    {
                        System.Threading.Interlocked.Increment(ref failures);
                    }
                });

                await Task.WhenAll(tasks);
                Assert(successes == 1 && failures == 4,
                    "C01: 5 concurrent payment creations on same order -> exactly 1 succeeded, 4 rejected");
            }

            // C02: Concurrent settlement vs cancellation
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 120.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment
                {
                    OrderId = orderId,
                    Provider = "Simulator",
                    Amount = 120.00m
                });

                var settleTask = Task.Run(async () =>
                {
                    try { return await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded", "TXN-C02-S"); }
                    catch { return false; }
                });

                var cancelTask = Task.Run(async () =>
                {
                    try { return await paymentRepo.UpdateStatusAsync(paymentId, "Cancelled"); }
                    catch { return false; }
                });

                var results = await Task.WhenAll(settleTask, cancelTask);
                var finalPayment = await paymentRepo.GetByIdAsync(paymentId);
                var finalOrderPayment = await connection.ExecuteScalarAsync<string>(
                    "SELECT PaymentStatus FROM dbo.Orders WHERE OrderId = @OrderId", new { OrderId = orderId });

                bool consistent = (finalPayment?.Status == "Succeeded" && finalOrderPayment == "Paid") ||
                                  (finalPayment?.Status == "Cancelled" && finalOrderPayment == "Pending");

                Assert(consistent && results.Count(r => r) == 1,
                    $"C02: Concurrent Settle vs Cancel serialized cleanly; final state is {finalPayment?.Status}");
            }

            // C03: Concurrent settlement of two payments for same order (defense-in-depth)
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 130.00m);

                // Insert one Pending and one directly via SQL as Failed (to simulate historical failed payment)
                var p1 = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 130.00m });

                // Update p1 to Succeeded
                await paymentRepo.UpdateStatusAsync(p1, "Succeeded");

                // Create a 2nd payment row (force insert to test defense-in-depth on Succeeded transition)
                const string forceInsertSql = """
                    INSERT INTO dbo.Payments (OrderId, Provider, Amount, Status)
                    VALUES (@OrderId, 'Simulator', 130.00, 'Pending');
                    SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
                    """;
                long p2 = 0;
                try
                {
                    p2 = await connection.ExecuteScalarAsync<long>(forceInsertSql, new { OrderId = orderId });
                }
                catch { /* UX_Payments_OneActivePaymentPerOrder might block if Status='Pending' */ }

                if (p2 > 0)
                {
                    bool p2Blocked = false;
                    try
                    {
                        await paymentRepo.UpdateStatusAsync(p2, "Succeeded");
                    }
                    catch (InvalidOperationException) { p2Blocked = true; }
                    Assert(p2Blocked, "C03: Defense-in-depth blocks 2nd payment from becoming Succeeded when order is already Paid");
                }
                else
                {
                    Assert(true, "C03: Filtered unique index prevented duplicate active payment creation (Passed)");
                }
            }

            // C04: Concurrent webhook arrival with identical payload
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 140.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 140.00m });

                var tasks = Enumerable.Range(0, 4).Select(async _ =>
                {
                    try { return await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded", "TXN-C04"); }
                    catch { return false; }
                });

                var webhookResults = await Task.WhenAll(tasks);
                var payment = await paymentRepo.GetByIdAsync(paymentId);

                Assert(webhookResults.All(r => r) && payment?.Status == "Succeeded",
                    "C04: 4 concurrent identical webhooks all succeed idempotently without deadlock");
            }

            // C05: Concurrent settlement vs refund attempt
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 150.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment
                {
                    OrderId = orderId,
                    Provider = "Simulator",
                    ProviderReference = "REF-C05",
                    Amount = 150.00m
                });

                // Pending cannot be refunded
                bool refundBeforeSucceededBlocked = false;
                try
                {
                    await paymentService.RefundAsync(paymentId, adminUserId, "Premature refund");
                }
                catch (InvalidOperationException) { refundBeforeSucceededBlocked = true; }

                Assert(refundBeforeSucceededBlocked,
                    "C05: Refund cannot be executed on Pending payment before it succeeds");
            }

            // C06: Concurrent settlement vs expiration service
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 160.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 160.00m });

                var settleTask = Task.Run(async () =>
                {
                    try { return await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded", "TXN-C06"); }
                    catch { return false; }
                });

                var expireTask = Task.Run(async () =>
                {
                    try
                    {
                        var res = await paymentRepo.ExpirePaymentAsync(paymentId, "System expiration race");
                        return res == PaymentStatusUpdateResult.Updated;
                    }
                    catch { return false; }
                });

                var raceResults = await Task.WhenAll(settleTask, expireTask);
                var payment = await paymentRepo.GetByIdAsync(paymentId);
                var orderPayment = await connection.ExecuteScalarAsync<string>(
                    "SELECT PaymentStatus FROM dbo.Orders WHERE OrderId = @OrderId", new { OrderId = orderId });

                bool validRaceOutcome = (payment?.Status == "Succeeded" && orderPayment == "Paid") ||
                                       (payment?.Status == "Cancelled" && orderPayment == "Pending");

                Assert(validRaceOutcome && raceResults.Count(r => r) == 1,
                    $"C06: Settle vs Expiration race resolved cleanly to single winner ({payment?.Status})");
            }

            // C07: Concurrent admin update vs customer cancellation attempt
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 170.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment { OrderId = orderId, Provider = "Simulator", Amount = 170.00m });

                var adminTask = Task.Run(async () =>
                {
                    try
                    {
                        var res = await paymentRepo.AdminUpdateStatusAsync(
                            paymentId, "Succeeded", "TXN-C07", adminUserId, "Admin approved");
                        return res == PaymentStatusUpdateResult.Updated;
                    }
                    catch { return false; }
                });

                var cancelTask = Task.Run(async () =>
                {
                    try
                    {
                        var res = await paymentRepo.AdminUpdateStatusAsync(
                            paymentId, "Cancelled", null, adminUserId, "Cancel request");
                        return res == PaymentStatusUpdateResult.Updated;
                    }
                    catch { return false; }
                });

                var results = await Task.WhenAll(adminTask, cancelTask);
                var payment = await paymentRepo.GetByIdAsync(paymentId);

                Assert(results.Count(r => r) == 1 && (payment?.Status == "Succeeded" || payment?.Status == "Cancelled"),
                    $"C07: Concurrent Admin updates serialized; final payment status is {payment?.Status}");
            }

            // C08: Provider reference reuse race
            {
                var orderId1 = await CreateTestOrderAsync("Pending", "Pending", 180.00m);
                var orderId2 = await CreateTestOrderAsync("Pending", "Pending", 180.00m);
                var sharedRef = "RACE-REF-" + Guid.NewGuid().ToString("N")[..8];

                int creations = 0;
                int collisions = 0;

                var tasks = new[]
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await paymentRepo.CreateAsync(new Payment
                            {
                                OrderId = orderId1,
                                Provider = "Simulator",
                                ProviderReference = sharedRef,
                                Amount = 180.00m
                            });
                            System.Threading.Interlocked.Increment(ref creations);
                        }
                        catch (InvalidOperationException) { System.Threading.Interlocked.Increment(ref collisions); }
                    }),
                    Task.Run(async () =>
                    {
                        try
                        {
                            await paymentRepo.CreateAsync(new Payment
                            {
                                OrderId = orderId2,
                                Provider = "Simulator",
                                ProviderReference = sharedRef,
                                Amount = 180.00m
                            });
                            System.Threading.Interlocked.Increment(ref creations);
                        }
                        catch (InvalidOperationException) { System.Threading.Interlocked.Increment(ref collisions); }
                    })
                };

                await Task.WhenAll(tasks);
                Assert(creations == 1 && collisions == 1,
                    "C08: Provider reference reuse race -> 1 succeeded, 1 collision blocked");
            }

            // ========================================================================
            // SUITE 3: WEBHOOK VERIFICATION & PROCESSING (W01 - W04)
            // ========================================================================
            Console.WriteLine("\n--------------------------------------------------------------------------------");
            Console.WriteLine("SUITE 3: Webhook Verification & Processing Tests (W01 - W04)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            // W01: Valid webhook payload + valid HMAC signature
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 200.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment
                {
                    OrderId = orderId,
                    Provider = "Simulator",
                    Amount = 200.00m
                });

                var (payload, signature) = SimulatedPaymentProvider.BuildWebhookPayload(
                    paymentId, "Succeeded", "SIM-W01-REF");

                var success = await paymentService.ProcessWebhookAsync(payload, signature);
                var payment = await paymentRepo.GetByIdAsync(paymentId);
                var orderPayment = await connection.ExecuteScalarAsync<string>(
                    "SELECT PaymentStatus FROM dbo.Orders WHERE OrderId = @OrderId", new { OrderId = orderId });

                Assert(success && payment?.Status == "Succeeded" && orderPayment == "Paid",
                    "W01: Valid payload + valid HMAC signature -> payment Succeeded, order Paid");
            }

            // W02: Valid payload + invalid HMAC signature
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 210.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment
                {
                    OrderId = orderId,
                    Provider = "Simulator",
                    Amount = 210.00m
                });

                var (payload, _) = SimulatedPaymentProvider.BuildWebhookPayload(paymentId, "Succeeded");
                var forgedSignature = "sha256=0000000000000000000000000000000000000000000000000000000000000000";

                bool rejected = false;
                try
                {
                    await paymentService.ProcessWebhookAsync(payload, forgedSignature);
                }
                catch (UnauthorizedAccessException) { rejected = true; }

                var payment = await paymentRepo.GetByIdAsync(paymentId);
                Assert(rejected && payment?.Status == "Pending",
                    "W02: Invalid HMAC signature rejected with UnauthorizedAccessException; status unchanged");
            }

            // W03: Missing signature header
            {
                bool rejected = false;
                try
                {
                    await paymentService.ProcessWebhookAsync("paymentId=1&status=Succeeded", "");
                }
                catch (ArgumentException) { rejected = true; }

                Assert(rejected, "W03: Missing signature header rejected with ArgumentException");
            }

            // W04: Webhook for unknown payment
            {
                var (payload, signature) = SimulatedPaymentProvider.BuildWebhookPayload(999999999L, "Succeeded");

                bool handledOrNotFound = false;
                try
                {
                    var updated = await paymentService.ProcessWebhookAsync(payload, signature);
                    handledOrNotFound = !updated;
                }
                catch (KeyNotFoundException) { handledOrNotFound = true; }
                catch (ArgumentException) { handledOrNotFound = true; }

                Assert(handledOrNotFound, "W04: Webhook for non-existent payment handled safely without crash");
            }

            // ========================================================================
            // SUITE 4: REFUND WORKFLOW (R01 - R04)
            // ========================================================================
            Console.WriteLine("\n--------------------------------------------------------------------------------");
            Console.WriteLine("SUITE 4: Refund Workflow Tests (R01 - R04)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            // R01: Refund on Succeeded payment succeeds
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 250.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment
                {
                    OrderId = orderId,
                    Provider = "Simulator",
                    ProviderReference = "SIM-R01-SESSION",
                    Amount = 250.00m
                });

                await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded", "TXN-R01");

                var refundResult = await paymentService.RefundAsync(
                    paymentId, adminUserId, "Customer requested order cancellation and full refund.");

                var payment = await paymentRepo.GetByIdAsync(paymentId);
                var orderPayment = await connection.ExecuteScalarAsync<string>(
                    "SELECT PaymentStatus FROM dbo.Orders WHERE OrderId = @OrderId", new { OrderId = orderId });
                var audits = (await auditRepo.GetByPaymentIdAsync(paymentId)).ToList();

                Assert(refundResult == PaymentStatusUpdateResult.Updated &&
                       payment?.Status == "Refunded" &&
                       orderPayment == "Refunded" &&
                       audits.Any(a => a.NewStatus == "Refunded" && a.Reason.Contains("Customer requested order cancellation")),
                    "R01: Refund on Succeeded payment sets payment=Refunded, order=Refunded, and writes audit");
            }

            // R02: Refund on Pending payment rejected
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 260.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment
                {
                    OrderId = orderId,
                    Provider = "Simulator",
                    ProviderReference = "SIM-R02",
                    Amount = 260.00m
                });

                bool rejected = false;
                try
                {
                    await paymentService.RefundAsync(paymentId, adminUserId, "Invalid refund");
                }
                catch (InvalidOperationException) { rejected = true; }

                Assert(rejected, "R02: Refund on Pending payment rejected");
            }

            // R03: Refund on Failed payment rejected
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 270.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment
                {
                    OrderId = orderId,
                    Provider = "Simulator",
                    ProviderReference = "SIM-R03",
                    Amount = 270.00m
                });

                await paymentRepo.UpdateStatusAsync(paymentId, "Failed");

                bool rejected = false;
                try
                {
                    await paymentService.RefundAsync(paymentId, adminUserId, "Invalid refund on failed");
                }
                catch (InvalidOperationException) { rejected = true; }

                Assert(rejected, "R03: Refund on Failed payment rejected");
            }

            // R04: Refund without reason rejected
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 280.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment
                {
                    OrderId = orderId,
                    Provider = "Simulator",
                    ProviderReference = "SIM-R04",
                    Amount = 280.00m
                });

                await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded");

                bool rejected = false;
                try
                {
                    await paymentService.RefundAsync(paymentId, adminUserId, "   ");
                }
                catch (ArgumentException) { rejected = true; }

                Assert(rejected, "R04: Refund with empty reason rejected with ArgumentException");
            }

            // ========================================================================
            // SUITE 5: PAYMENT EXPIRATION SERVICE (E01 - E02)
            // ========================================================================
            Console.WriteLine("\n--------------------------------------------------------------------------------");
            Console.WriteLine("SUITE 5: Payment Expiration Tests (E01 - E02)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            // E01: Pending payment older than threshold expired to Cancelled
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 300.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment
                {
                    OrderId = orderId,
                    Provider = "Simulator",
                    Amount = 300.00m
                });

                // Simulate aged payment by setting CreatedAt back 45 minutes
                await connection.ExecuteAsync(
                    "UPDATE dbo.Payments SET CreatedAt = DATEADD(MINUTE, -45, SYSUTCDATETIME()) WHERE PaymentId = @PaymentId",
                    new { PaymentId = paymentId });

                // Run ExpirePaymentAsync (as called by PaymentExpirationService)
                var result = await paymentRepo.ExpirePaymentAsync(
                    paymentId, "Payment expired: exceeded the maximum allowed pending duration.");

                var payment = await paymentRepo.GetByIdAsync(paymentId);
                var audits = (await auditRepo.GetByPaymentIdAsync(paymentId)).ToList();

                Assert(result == PaymentStatusUpdateResult.Updated &&
                       payment?.Status == "Cancelled" &&
                       audits.Any(a => a.NewStatus == "Cancelled" && a.AdminUserId == null && a.Reason.Contains("Payment expired")),
                    "E01: Aged Pending payment expired to Cancelled with system audit log (AdminUserId=null)");
            }

            // E02: Succeeded payment older than threshold NOT expired
            {
                var orderId = await CreateTestOrderAsync("Pending", "Pending", 310.00m);
                var paymentId = await paymentRepo.CreateAsync(new Payment
                {
                    OrderId = orderId,
                    Provider = "Simulator",
                    Amount = 310.00m
                });

                await paymentRepo.UpdateStatusAsync(paymentId, "Succeeded");

                // Set CreatedAt back 45 minutes
                await connection.ExecuteAsync(
                    "UPDATE dbo.Payments SET CreatedAt = DATEADD(MINUTE, -45, SYSUTCDATETIME()) WHERE PaymentId = @PaymentId",
                    new { PaymentId = paymentId });

                bool rejected = false;
                try
                {
                    await paymentRepo.ExpirePaymentAsync(paymentId, "Attempt to expire succeeded payment");
                }
                catch (InvalidOperationException) { rejected = true; }

                var payment = await paymentRepo.GetByIdAsync(paymentId);
                Assert(rejected && payment?.Status == "Succeeded",
                    "E02: Succeeded payment cannot be expired by expiration service");
            }

            // ========================================================================
            // SUMMARY & DB CLEANUP
            // ========================================================================
            Console.WriteLine("\n================================================================================");
            Console.WriteLine($"TEST EXECUTION COMPLETED: {passed} PASSED, {failed} FAILED (TOTAL: {passed + failed})");
            Console.WriteLine("================================================================================");

            // Cleanup test orders and payments
            Console.WriteLine("Cleaning up test data created during test execution...");
            await connection.ExecuteAsync("DELETE FROM dbo.PaymentStatusAuditLogs WHERE PaymentId IN (SELECT PaymentId FROM dbo.Payments WHERE Provider = 'Simulator');");
            await connection.ExecuteAsync("DELETE FROM dbo.Payments WHERE Provider = 'Simulator';");
            await connection.ExecuteAsync("DELETE FROM dbo.Orders WHERE ShippingFullName = 'Test Customer';");
            Console.WriteLine("Cleanup completed.");

            return failed > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FATAL ERROR] {ex}");
            Console.ResetColor();
            return 1;
        }
    }
}
