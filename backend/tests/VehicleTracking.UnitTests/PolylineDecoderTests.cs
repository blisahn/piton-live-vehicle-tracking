using VehicleTracking.Infrastructure.Transit;

namespace VehicleTracking.UnitTests;

public class PolylineDecoderTests
{
    [Fact]
    public void Decodes_the_example_from_the_google_documentation()
    {
        var points = PolylineDecoder.Decode("_p~iF~ps|U_ulLnnqC_mqNvxq`@");

        (double Lat, double Lon)[] expected = [(38.5, -120.2), (40.7, -120.95), (43.252, -126.453)];
        Assert.Equal(expected.Length, points.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Lat, points[i].Latitude, 5);
            Assert.Equal(expected[i].Lon, points[i].Longitude, 5);
        }
    }

    [Fact]
    public void An_empty_string_gives_no_points()
    {
        Assert.Empty(PolylineDecoder.Decode(""));
    }
}
