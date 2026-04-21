using Amazon.CDK;

namespace Dashboard.Stack
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new DashboardStack(app, "AiDashboardStack", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                    Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION") ?? "us-east-1",
                },
                Description = "AI Personal Dashboard — Lambda + Step Functions + Bedrock"
            });
            app.Synth();
        }
    }
}
