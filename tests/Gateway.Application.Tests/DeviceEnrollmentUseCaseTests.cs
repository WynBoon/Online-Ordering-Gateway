using Gateway.Application.Repositories;
using Gateway.Application.UseCases;
using Gateway.Domain.Devices;
using Gateway.Domain.Enums;
using Gateway.Domain.Tenancy;
using Moq;

namespace Gateway.Application.Tests;

public class DeviceEnrollmentUseCaseTests
{
    [Fact]
    public async Task Issue_snapshots_store_default_functions_and_hashes_six_digit_code()
    {
        var storeId = Guid.NewGuid();
        var store = new Store
        {
            Id = storeId,
            Name = "Test Store",
            Timezone = "Africa/Johannesburg",
            State = StoreState.Active,
            DefaultDeviceFunctions = DeviceFunctionSets.KitchenBackup
        };

        var storeRepo = new Mock<IStoreRepository>();
        storeRepo.Setup(r => r.GetByIdAsync(storeId, It.IsAny<CancellationToken>())).ReturnsAsync(store);

        StoreDevice? saved = null;
        var deviceRepo = new Mock<IStoreDeviceRepository>();
        deviceRepo
            .Setup(r => r.AddAsync(It.IsAny<StoreDevice>(), It.IsAny<CancellationToken>()))
            .Callback<StoreDevice, CancellationToken>((d, _) => saved = d)
            .Returns(Task.CompletedTask);

        var useCase = new DeviceEnrollmentUseCase(deviceRepo.Object, storeRepo.Object);
        var issued = await useCase.IssueAsync(storeId, functions: null, name: "Pass kitchen", CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(6, issued.EnrollmentCode.Length);
        Assert.True(issued.EnrollmentCode.All(char.IsDigit));
        Assert.Equal(DeviceSecrets.Hash(issued.EnrollmentCode), saved!.EnrollmentCodeHash);
        Assert.Equal(DeviceFunctionSets.KitchenBackup, saved.Functions);
        Assert.Equal(DeviceStatus.PendingEnrollment, saved.Status);
        Assert.Equal("Pass kitchen", saved.Name);
        Assert.Null(saved.TokenHash);
    }

    [Fact]
    public async Task Claim_returns_token_activates_clears_code_hash_and_uses_new_name()
    {
        var storeId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var code = "483920";
        var pending = new StoreDevice
        {
            Id = deviceId,
            StoreId = storeId,
            Name = "Pass kitchen",
            Status = DeviceStatus.PendingEnrollment,
            Functions = DeviceFunctionSets.KitchenStatusOfRecord,
            EnrollmentCodeHash = DeviceSecrets.Hash(code),
            EnrollmentExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        var store = new Store
        {
            Id = storeId,
            Name = "Local Dev Kitchen",
            Timezone = "Africa/Johannesburg",
            State = StoreState.Active
        };

        var deviceRepo = new Mock<IStoreDeviceRepository>();
        deviceRepo
            .Setup(r => r.GetPendingByCodeHashAsync(DeviceSecrets.Hash(code), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        deviceRepo
            .Setup(r => r.SaveAsync(pending, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var storeRepo = new Mock<IStoreRepository>();
        storeRepo.Setup(r => r.GetByIdAsync(storeId, It.IsAny<CancellationToken>())).ReturnsAsync(store);

        var useCase = new DeviceEnrollmentUseCase(deviceRepo.Object, storeRepo.Object);
        var claimed = await useCase.ClaimAsync(code, "Front tablet", "android-emu-1", CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(claimed.DeviceToken));
        Assert.Equal(deviceId, claimed.DeviceId);
        Assert.Equal("Front tablet", claimed.DeviceName);
        Assert.Equal("Local Dev Kitchen", claimed.StoreName);
        Assert.Equal(DeviceStatus.Active, pending.Status);
        Assert.Null(pending.EnrollmentCodeHash);
        Assert.Equal(DeviceSecrets.Hash(claimed.DeviceToken), pending.TokenHash);
        Assert.Equal("android-emu-1", pending.HardwareFingerprint);
        Assert.NotNull(pending.EnrolledAtUtc);
    }

    [Fact]
    public async Task Claim_expired_throws_containing_expired_and_sets_revoked()
    {
        var code = "111111";
        var pending = new StoreDevice
        {
            StoreId = Guid.NewGuid(),
            Name = "Stale",
            Status = DeviceStatus.PendingEnrollment,
            EnrollmentCodeHash = DeviceSecrets.Hash(code),
            EnrollmentExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var deviceRepo = new Mock<IStoreDeviceRepository>();
        deviceRepo
            .Setup(r => r.GetPendingByCodeHashAsync(DeviceSecrets.Hash(code), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        deviceRepo
            .Setup(r => r.SaveAsync(pending, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var useCase = new DeviceEnrollmentUseCase(deviceRepo.Object, Mock.Of<IStoreRepository>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ClaimAsync(code, "x", null, CancellationToken.None));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DeviceStatus.Revoked, pending.Status);
        Assert.Null(pending.EnrollmentCodeHash);
        deviceRepo.Verify(r => r.SaveAsync(pending, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Remove_deletes_revoked_device()
    {
        var device = new StoreDevice
        {
            Id = Guid.NewGuid(),
            StoreId = Guid.NewGuid(),
            Name = "Old tablet",
            Status = DeviceStatus.Revoked,
            RevokedAtUtc = DateTimeOffset.UtcNow
        };

        var deviceRepo = new Mock<IStoreDeviceRepository>();
        deviceRepo.Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        deviceRepo.Setup(r => r.DeleteAsync(device, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var useCase = new DeviceEnrollmentUseCase(deviceRepo.Object, Mock.Of<IStoreRepository>());
        await useCase.RemoveAsync(device.Id, CancellationToken.None);

        deviceRepo.Verify(r => r.DeleteAsync(device, It.IsAny<CancellationToken>()), Times.Once);
        deviceRepo.Verify(r => r.SaveAsync(It.IsAny<StoreDevice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Remove_throws_if_device_is_not_revoked()
    {
        var device = new StoreDevice
        {
            Id = Guid.NewGuid(),
            StoreId = Guid.NewGuid(),
            Name = "Live tablet",
            Status = DeviceStatus.Active
        };

        var deviceRepo = new Mock<IStoreDeviceRepository>();
        deviceRepo.Setup(r => r.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);

        var useCase = new DeviceEnrollmentUseCase(deviceRepo.Object, Mock.Of<IStoreRepository>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.RemoveAsync(device.Id, CancellationToken.None));

        Assert.Contains("Revoke", ex.Message, StringComparison.OrdinalIgnoreCase);
        deviceRepo.Verify(r => r.DeleteAsync(It.IsAny<StoreDevice>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
