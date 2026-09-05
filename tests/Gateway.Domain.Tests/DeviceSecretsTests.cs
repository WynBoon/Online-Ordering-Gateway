using Gateway.Domain.Devices;

namespace Gateway.Domain.Tests;

public class DeviceSecretsTests
{
    [Fact]
    public void NewEnrollmentCode_is_six_digits()
    {
        for (var i = 0; i < 20; i++)
        {
            var code = DeviceSecrets.NewEnrollmentCode();
            Assert.Equal(6, code.Length);
            Assert.True(code.All(char.IsDigit), code);
        }
    }

    [Fact]
    public void Hash_is_stable_not_plaintext_and_length_64()
    {
        const string value = "483920";
        var hash = DeviceSecrets.Hash(value);

        Assert.Equal(64, hash.Length);
        Assert.NotEqual(value, hash);
        Assert.Equal(hash, DeviceSecrets.Hash(value));
        Assert.True(hash.All(c => char.IsAsciiHexDigit(c)), hash);
    }
}
