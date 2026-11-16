# 正交相机。方位角从 Unity +Z 向 +X 旋转，用逗号分隔。
# 第一个角度对应 view0.png，也是两圈的起点。
reference_azimuths='30,210'
reference_elevation=15         # 仰角，单位度，范围 (-90, 90)
frame_fill=0.9                  # 整圈取景占框比例 (0, 1]
orbit_direction=increasing     # increasing 或 decreasing，按 renderer 方位角增减
