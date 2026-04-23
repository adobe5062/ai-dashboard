namespace Dashboard.Stack
{
    public static class StackConfig
    {
        public const string SummariesTableName = "dashboard-summaries";
        public const string RemindersTableName = "dashboard-reminders";
        public const string S3BucketName = "dashboard-raw-data";
        public const string BedrockModelId = "us.anthropic.claude-haiku-4-5-20251001-v1:0";
        public const string BedrockRegion = "us-east-1";
        public const string GitHubUsername = "adobe5062";

        // SSM Parameter Store paths
        public const string SsmWeatherKey = "/dashboard/openweather/api-key";
        public const string SsmWeatherLat = "/dashboard/openweather/lat";
        public const string SsmWeatherLon = "/dashboard/openweather/lon";
        public const string SsmSteamKey = "/dashboard/steam/api-key";
        public const string SsmSteamUserId = "/dashboard/steam/user-id";
        public const string SsmTmdbKey = "/dashboard/tmdb/api-key";

        // API Gateway throttling
        public const int ApiRateLimitPerMinute = 60;
        public const int ApiBurstLimit = 10;

        // Lambda concurrency cap
        public const int LambdaReservedConcurrency = 5;

        // CloudWatch spend alert threshold (USD)
        public const double SpendAlertThresholdUsd = 5.0;
    }
}
