using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.Testing.Serializers;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(TestCaseSerializer),
    typeof(IGrillPlanner),
    typeof(GreedyShelfPlanner),
    typeof(ExactBacktrackingPlanner),
    typeof(OptimizedHeuristicPlanner),
    typeof(MaxRectsPlanner),
    typeof(GuillotinePlanner),
    typeof(BatchPlanner),
    typeof(OrToolsPlanner),
    typeof(PortfolioPlanner))]