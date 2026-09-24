using GrillMaster.Application.Features.Plans;
using GrillMaster.Testing.Serializers;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(TestCaseSerializer),
    typeof(GrillPlanner))]
