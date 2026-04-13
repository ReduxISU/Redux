using Xunit;
using API.Problems.NPComplete.NPC_CONVEXHULL;
using API.Problems.NPComplete.NPC_CONVEXHULL.Verifiers;
using API.Problems.NPComplete.NPC_CONVEXHULL.Solvers;
using API.Interfaces;

namespace redux_tests;

public class CONVEXHULL_Tests
{

    [Fact]
    public void CONVEXHULL_Default_Instantiation()
    {
        CONVEXHULL hull = new CONVEXHULL();
        string actual_result = hull.defaultSolver.solve(hull);
        Assert.Equal(actual_result, "((0.2723211656942368,-0.8053758131859647), (0.7674622377407927,-0.21537444528240846), (0.6077591838324792,0.5288040272918157), (-0.32705115386597394,0.6744065707101621), (-0.6984449872706371,0.3857380723376367), (-0.9308276577586132,-0.1423800479224624), (-0.27394905790800017,-0.7488048223660126))");
    }

    [Fact]
    public void CONVEXHULL_Custom_String_Input_Test()
    {
        string input = "{(0.14910331775506291, -0.8406444850820369), (0.07257532737672512, -0.251706857318015), (0.1144445878616871, 0.41987963593139965), (-0.5016809602104544, 0.4032473346040213), (0.3928123439811402, -0.8253116563139804), (0.36781650107298325, -0.24266892313868738), (0.2782909741500923, 0.2851627802061465), (0.7051482337938415, -0.45763755657931227), (0.14071041744097257, 0.3448575040369797), (0.7381887883615372, 0.38173355278244414)}";
        string expected_result = "((0.3928123439811402,-0.8253116563139804), (0.7051482337938415,-0.45763755657931227), (0.7381887883615372,0.38173355278244414), (0.1144445878616871,0.41987963593139965), (-0.5016809602104544,0.4032473346040213), (0.14910331775506291,-0.8406444850820369))";
        CONVEXHULL hull = new CONVEXHULL(input);
        string actual_result = hull.defaultSolver.solve(hull);
        Assert.Equal(expected_result, actual_result);
    }

    [Theory] //Test convex hull verifier with a few certificates

    [InlineData("{(-0.17055262956662953,-0.34879784136536185), (0.5628748918213995,0.2992518362152232), (0.5506724939545227,0.19610502706432276), (-0.6314260885584542,-0.7142578381816922), (0.29261853988332254,-0.6103940921033697)}", "((0.29261853988332254,-0.6103940921033697), (0.5506724939545227,0.19610502706432276), (0.5628748918213995,0.2992518362152232), (-0.6314260885584542,-0.7142578381816922))", true)]
    [InlineData("{(0,0),(2,0),(1,2)}", "((0,0), (2,0), (1,2))", true)]
    [InlineData("{(0,1),(2,5),(1,4)}", "((2,5), (1,4))", false)]
    [InlineData("{(1,1),(0,0),(2,2),(3,3)}", "((3,3),(0,0))", true)]
    [InlineData("{(0,0), (2,0), (2,2), (0,2), (1,1)}", "((0,0), (2,0), (2,2), (1,1), (0,2))", false)]
    public void CONVEXHULL_verifier(string instance, string certificate, bool expected)
    {
        CONVEXHULL convexHull = new CONVEXHULL(instance);
        bool result = convexHull.defaultVerifier.verify(convexHull, certificate);
        Assert.Equal(expected, result);

    }


    [Theory] //test solver
    [InlineData("{(-1,-1), (1,-1), (1,1), (-1,1), (0,0), (0.5,0.2)}", "((1,-1), (1,1), (-1,1), (-1,-1))")]
    [InlineData("{(0,0), (2,1), (3,3), (1,4), (-1,2), (1,2)}", "((2,1), (3,3), (1,4), (-1,2), (0,0))")]
    public void CONVEXHULL_solver(string instance, string certificate)
    {
        CONVEXHULL convexHull = new CONVEXHULL(instance);
        ConvexHullSolver solver = convexHull.defaultSolver;
        string solvedString = solver.solve(convexHull);
        Assert.Equal(certificate, solvedString);
    }

}