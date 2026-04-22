using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Amazon.CDK.CustomResources;
using Constructs;
using System.Collections.Generic;

namespace Dashboard.Stack
{
    public class DashboardStack : Amazon.CDK.Stack
    {
        internal DashboardStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // ── Storage ────────────────────────────────────────────────────

            var rawDataBucket = new Bucket(this, "RawDataBucket", new BucketProps
            {
                BucketName = $"{StackConfig.S3BucketName}-{this.Account}",
                RemovalPolicy = RemovalPolicy.DESTROY,
                AutoDeleteObjects = true,
            });

            var summariesTable = new Table(this, "SummariesTable", new TableProps
            {
                TableName = StackConfig.SummariesTableName,
                PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "date", Type = AttributeType.STRING },
                TimeToLiveAttribute = "ttl",
                RemovalPolicy = RemovalPolicy.DESTROY,
                BillingMode = BillingMode.PAY_PER_REQUEST,
            });

            var remindersTable = new Table(this, "RemindersTable", new TableProps
            {
                TableName = StackConfig.RemindersTableName,
                PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "id", Type = AttributeType.STRING },
                RemovalPolicy = RemovalPolicy.DESTROY,
                BillingMode = BillingMode.PAY_PER_REQUEST,
            });

            // ── Shared Lambda environment ──────────────────────────────────

            var sharedEnv = new Dictionary<string, string>
            {
                ["SUMMARIES_TABLE"] = StackConfig.SummariesTableName,
                ["REMINDERS_TABLE"] = StackConfig.RemindersTableName,
                ["S3_BUCKET"] = rawDataBucket.BucketName,
                ["BEDROCK_MODEL_ID"] = StackConfig.BedrockModelId,
                ["BEDROCK_REGION"] = StackConfig.BedrockRegion,
                ["GITHUB_USERNAME"] = StackConfig.GitHubUsername,
                ["SSM_WEATHER_KEY"] = StackConfig.SsmWeatherKey,
                ["SSM_WEATHER_LAT"] = StackConfig.SsmWeatherLat,
                ["SSM_WEATHER_LON"] = StackConfig.SsmWeatherLon,
                ["SSM_STEAM_KEY"] = StackConfig.SsmSteamKey,
                ["SSM_STEAM_USER_ID"] = StackConfig.SsmSteamUserId,
            };

            // ── Lambda 1: Data Fetcher ─────────────────────────────────────

            var dataFetcherFn = new Function(this, "DataFetcherFn", new FunctionProps
            {
                FunctionName = "dashboard-data-fetcher",
                Runtime = Runtime.DOTNET_8,
                Handler = "Dashboard.DataFetcher::Dashboard.DataFetcher.Function::FunctionHandler",
                Code = Code.FromAsset("../src/Dashboard.DataFetcher/bin/Release/net9.0/linux-x64/publish"),
                Timeout = Duration.Seconds(60),
                MemorySize = 512,
                ReservedConcurrentExecutions = StackConfig.LambdaReservedConcurrency,
                Environment = sharedEnv,
            });

            rawDataBucket.GrantPut(dataFetcherFn);
            dataFetcherFn.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = ["ssm:GetParameter"],
                Resources = [$"arn:aws:ssm:{this.Region}:{this.Account}:parameter/dashboard/*"],
            }));

            // ── Lambda 2: Bedrock Summarizer ───────────────────────────────

            var summarizerFn = new Function(this, "SummarizerFn", new FunctionProps
            {
                FunctionName = "dashboard-summarizer",
                Runtime = Runtime.DOTNET_8,
                Handler = "Dashboard.Summarizer::Dashboard.Summarizer.Function::FunctionHandler",
                Code = Code.FromAsset("../src/Dashboard.Summarizer/bin/Release/net9.0/linux-x64/publish"),
                Timeout = Duration.Seconds(60),
                MemorySize = 512,
                ReservedConcurrentExecutions = StackConfig.LambdaReservedConcurrency,
                Environment = sharedEnv,
            });

            rawDataBucket.GrantRead(summarizerFn);
            summariesTable.GrantWriteData(summarizerFn);
            summarizerFn.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = ["bedrock:InvokeModel"],
                Resources = [$"arn:aws:bedrock:{StackConfig.BedrockRegion}::foundation-model/{StackConfig.BedrockModelId}"],
            }));

            // ── Lambda 3: API Reader (read-only) ───────────────────────────

            var apiReaderFn = new Function(this, "ApiReaderFn", new FunctionProps
            {
                FunctionName = "dashboard-api-reader",
                Runtime = Runtime.DOTNET_8,
                Handler = "Dashboard.ApiReader::Dashboard.ApiReader.Function::FunctionHandler",
                Code = Code.FromAsset("../src/Dashboard.ApiReader/bin/Release/net9.0/linux-x64/publish"),
                Timeout = Duration.Seconds(10),
                MemorySize = 256,
                ReservedConcurrentExecutions = StackConfig.LambdaReservedConcurrency,
                Environment = sharedEnv,
            });

            summariesTable.GrantReadData(apiReaderFn);
            remindersTable.GrantReadData(apiReaderFn);

            // ── Step Functions pipeline ────────────────────────────────────

            var fetchTask = new LambdaInvoke(this, "FetchDataTask", new LambdaInvokeProps
            {
                LambdaFunction = dataFetcherFn,
                OutputPath = "$.Payload",
            });

            var summarizeTask = new LambdaInvoke(this, "SummarizeTask", new LambdaInvokeProps
            {
                LambdaFunction = summarizerFn,
                OutputPath = "$.Payload",
            });

            var pipeline = new StateMachine(this, "DashboardPipeline", new StateMachineProps
            {
                StateMachineName = "dashboard-daily-pipeline",
                DefinitionBody = DefinitionBody.FromChainable(fetchTask.Next(summarizeTask)),
                Timeout = Duration.Minutes(5),
            });

            // ── EventBridge schedule (daily 7am EST = 12:00 UTC) ──────────

            new Rule(this, "DailyTrigger", new RuleProps
            {
                RuleName = "dashboard-daily-7am",
                Schedule = Amazon.CDK.AWS.Events.Schedule.Cron(new Amazon.CDK.AWS.Events.CronOptions { Hour = "12", Minute = "0" }),
                Targets = [new SfnStateMachine(pipeline)],
            });

            // ── API Gateway ────────────────────────────────────────────────

            var api = new RestApi(this, "DashboardApi", new RestApiProps
            {
                RestApiName = "dashboard-api",
                DefaultCorsPreflightOptions = new CorsOptions
                {
                    AllowOrigins = Cors.ALL_ORIGINS,
                    AllowMethods = Cors.ALL_METHODS,
                },
                DeployOptions = new StageOptions
                {
                    ThrottlingRateLimit = StackConfig.ApiRateLimitPerMinute,
                    ThrottlingBurstLimit = StackConfig.ApiBurstLimit,
                    CachingEnabled = true,
                    CacheTtl = Duration.Hours(1),
                },
            });

            var dashboardResource = api.Root.AddResource("dashboard");
            dashboardResource.AddMethod("GET", new LambdaIntegration(apiReaderFn));

            // ── CloudWatch spend alert ─────────────────────────────────────

            var alertTopic = new Topic(this, "SpendAlertTopic", new TopicProps
            {
                TopicName = "dashboard-spend-alert",
                DisplayName = "Dashboard AWS Spend Alert",
            });

            var spendAlarm = new CfnAlarm(this, "SpendAlarm", new CfnAlarmProps
            {
                AlarmName = "dashboard-monthly-spend",
                AlarmDescription = $"Monthly spend exceeded ${StackConfig.SpendAlertThresholdUsd}",
                Namespace = "AWS/Billing",
                MetricName = "EstimatedCharges",
                Dimensions = new[] { new CfnAlarm.DimensionProperty { Name = "Currency", Value = "USD" } },
                Statistic = "Maximum",
                Period = 86400,
                EvaluationPeriods = 1,
                Threshold = StackConfig.SpendAlertThresholdUsd,
                ComparisonOperator = "GreaterThanThreshold",
                AlarmActions = [alertTopic.TopicArn],
                TreatMissingData = "notBreaching",
            });

            // ── Reminders seeder (runs once on deploy via custom resource) ─

            var reminders = new[]
            {
                new { id = "rem_001", title = "Vehicle oil change",       category = "vehicle", dueDate = "2026-05-05", recurring = "every 6 months" },
                new { id = "rem_002", title = "HVAC filter replacement",  category = "home",    dueDate = "2026-04-18", recurring = "every 3 months" },
                new { id = "rem_003", title = "Annual checkup",           category = "health",  dueDate = "2026-05-15", recurring = "annually"       },
                new { id = "rem_004", title = "Renew vehicle registration",category = "vehicle", dueDate = "2026-08-01", recurring = "annually"       },
                new { id = "rem_005", title = "Refrigerator water filter", category = "home",   dueDate = "2026-06-10", recurring = "every 6 months" },
            };

            foreach (var r in reminders)
            {
                new AwsCustomResource(this, $"Seed-{r.id}", new AwsCustomResourceProps
                {
                    OnCreate = new AwsSdkCall
                    {
                        Service = "DynamoDB",
                        Action = "putItem",
                        Parameters = new Dictionary<string, object>
                        {
                            ["TableName"] = StackConfig.RemindersTableName,
                            ["ConditionExpression"] = "attribute_not_exists(id)",
                            ["Item"] = new Dictionary<string, object>
                            {
                                ["id"]        = new Dictionary<string, string> { ["S"] = r.id },
                                ["title"]     = new Dictionary<string, string> { ["S"] = r.title },
                                ["category"]  = new Dictionary<string, string> { ["S"] = r.category },
                                ["dueDate"]   = new Dictionary<string, string> { ["S"] = r.dueDate },
                                ["recurring"] = new Dictionary<string, string> { ["S"] = r.recurring },
                            },
                        },
                        PhysicalResourceId = PhysicalResourceId.Of($"seed-reminder-{r.id}"),
                    },
                    Policy = AwsCustomResourcePolicy.FromSdkCalls(new SdkCallsPolicyOptions
                    {
                        Resources = AwsCustomResourcePolicy.ANY_RESOURCE,
                    }),
                });
            }

            // ── Outputs ────────────────────────────────────────────────────

            new CfnOutput(this, "ApiUrl", new CfnOutputProps
            {
                Value = api.Url + "dashboard",
                Description = "API Gateway endpoint — paste into frontend index.html",
            });

            new CfnOutput(this, "SummariesTableName", new CfnOutputProps
            {
                Value = summariesTable.TableName,
            });

            new CfnOutput(this, "SpendAlertTopicArn", new CfnOutputProps
            {
                Value = alertTopic.TopicArn,
                Description = "Subscribe your email here to receive spend alerts",
            });
        }
    }
}
