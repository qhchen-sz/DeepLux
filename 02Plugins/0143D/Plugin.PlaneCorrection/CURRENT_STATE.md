# Plugin.PlaneCorrection 当前状态

更新时间：2026-06-08

## 用户当前需求

用户要求把 `Plugin.PlaneCorrection` 平面校正插件改成三种校正方式，并可在界面选择：

1. 快速校正
2. 投影校正
3. 点到面距离图

前置讨论结论：

- 用户做的是 3D 视觉项目，倾斜基准面场景可能达到 30 度。
- 单纯 `Z校正 = Z原图 - 当前平面 + 基准平面` 只能做高度趋势补偿，不是真实 3D 投影校正。
- 对工业现场更严谨的方式是按真实 X/Y/Z 比例转成物理 XYZ，再做 3D 姿态校正。
- 用户要求“姿态被校正，距离/高度差不被抹掉”。

## 本次已修改文件

### `Views/PlaneCorrectionView.xaml`

新增界面参数：

- `校正方式` 单选框：
  - `快速校正`
  - `投影校正`
  - `点到面距离图`
- `分辨率设置`：
  - `X分辨率`
  - `Y分辨率`
  - `Z分辨率`

单选项使用：

```xml
IsChecked="{Binding CorrectionMode, Converter={StaticResource EnumConverter}, ConverterParameter={x:Static vm:eCorrectionMode.Quick}}"
```

三项已设置 `GroupName="CorrectionMode"`，并改成竖排，避免左侧 320 宽度拥挤。

### `ViewModels/PlaneCorrectionViewModel.cs`

新增枚举：

```csharp
public enum eCorrectionMode
{
    Quick,
    Projection,
    PointToPlaneDistance,
}
```

新增链接命令：

```csharp
InitResolutionX,
InitResolutionY,
InitResolutionZ,
```

新增属性：

```csharp
private eCorrectionMode _CorrectionMode = eCorrectionMode.Projection;
public eCorrectionMode CorrectionMode
{
    get { return _CorrectionMode; }
    set { Set(ref _CorrectionMode, value); }
}

public LinkVarModel InitResolutionX { get; set; } = new LinkVarModel() { Text = "1" };
public LinkVarModel InitResolutionY { get; set; } = new LinkVarModel() { Text = "1" };
public LinkVarModel InitResolutionZ { get; set; } = new LinkVarModel() { Text = "1" };
```

分辨率参数已接入：

- `Loaded()` 的 `TextChanged`
- `OnVarChanged()`
- `LinkCommand`
- `HVSerialize()`
- `HVDeserialize()`

## 三种校正逻辑现状

### 快速校正

使用高度图方式：

```csharp
residualObj = 原始高度图 - 当前拟合平面图
zCorrectedObj = residualObj + 基准平面图 + heightOffset + translateZ
```

特点：

- 快
- 保留当前工件相对基准面的高度差
- 不做真实 X/Y 几何投影

### 投影校正

流程：

1. 用 ROI 拟合当前平面。
2. 拟合/读取基准平面。
3. 根据 X/Y/Z 分辨率把 HALCON 一阶面参数转标准物理平面法向：

   ```text
   X = col * resolutionX
   Y = row * resolutionY
   Z = zPixel * resolutionZ
   ```

4. 生成 `X/Y/Z` 图像。
5. `XyzToObjectModel3d` 转点云。
6. `BuildPlaneAlignmentHomMat3D` 构造 3D 旋转矩阵，把当前平面法向旋到基准平面法向。
7. `AffineTransObjectModel3d` 做 3D 变换。
8. `ObjectModel3dToXyz(..., "from_xyz_map", ...)` 回写原高度图网格。
9. 输出 Z 图，再除回 `resolutionZ`，保持输出高度单位与输入一致。

注意：

- 已移除旧的 `RigidTransObjectModel3d`，避免之前日志里的 `Wrong number of values of control parameter 2`。
- `from_xyz_map` 适用于 `XyzToObjectModel3d` 生成的点云，CamParam/Pose 传空 tuple。
- 当前投影模式是围绕 ROI 中心旋转，所以不会把当前工件相对基准面的中心高度差抹掉。

### 点到面距离图

输出：

```csharp
zDiffToRefObj = 原始高度图 - 基准平面图 + translateZ
distanceObj = zDiffToRefObj * refNz
```

特点：

- 输出不是“姿态校正后的高度图”，而是每点到基准平面的法向距离图。
- 更适合平面度、凸起/凹陷、缺陷检测。

## 已做静态检查

已用 `rg` 和本地 `halcondotnet.dll` 反射确认：

- `HOperatorSet.GenImageSurfaceFirstOrder` 本地只有 HTuple 签名，无 HeightMeasurement 那类 double/HTuple 二义性。
- `HOperatorSet.XyzToObjectModel3d` 签名存在。
- `HOperatorSet.AffineTransObjectModel3d` 签名存在。
- `HOperatorSet.ObjectModel3dToXyz` 签名存在。
- `HOperatorSet.CopyObj` 签名存在。
- `HTuple(double[])` 构造存在，`BuildPlaneAlignmentHomMat3D` 返回矩阵写法可用。
- 当前 `Plugin.PlaneCorrection` 内无 `RigidTransObjectModel3d` 残留。
- 当前 `Plugin.PlaneCorrection` 内无 `residualDistanceObj` 残留。
- 当前 `Plugin.PlaneCorrection` 内无 `cartesian` 回退残留。

## 生命周期处理

`CorrectedImage` 输出后不释放 `zCorrectedObj`，避免下游图像句柄失效。

ROI 显示对象不直接传临时 `domain`，已改成：

```csharp
HOperatorSet.CopyObj(domain, out displayDomainObj, 1, -1);
```

并用 `displayDomainAssigned` 避免 finally 释放已交给显示列表保存的对象。

## 未做事项

用户之前明确要求后续不要主动编译，由用户自己编译。因此本次没有运行 `msbuild`。

下次优先事项：

1. 用户自己编译后，如果有编译错误，优先修 `PlaneCorrectionViewModel.cs` 和 `PlaneCorrectionView.xaml`。
2. 如果运行时报 `object_model_3d_to_xyz` 相关异常，重点检查 `from_xyz_map` 是否被当前 HALCON 版本支持，以及 `XyzToObjectModel3d` 后的 xyz_map 属性是否保留到 `AffineTransObjectModel3d` 后。
3. 如果投影校正效果不符合预期，重点验证：
   - X/Y/Z 分辨率是否为真实物理比例。
   - 高度图原始 Z 单位是否已经是 mm。
   - 手动基准平面 `Nx/Ny/Nz/D` 是否按物理坐标方程输入。
   - 旋转中心目前是当前 ROI 中心。
4. `点到面距离图` 当前输出单位跟输入高度图单位一致；如果用户希望强制输出物理单位，需要再乘/规范 `resolutionZ` 的语义。

## 相关文件

- `02Plugins\0143D\Plugin.PlaneCorrection\ViewModels\PlaneCorrectionViewModel.cs`
- `02Plugins\0143D\Plugin.PlaneCorrection\Views\PlaneCorrectionView.xaml`
- `02Plugins\0143D\Plugin.PlaneCorrection\Plugin.PlaneCorrection.csproj`

