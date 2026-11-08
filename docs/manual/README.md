# 装置手册骨架

物品 ID：`fluxforge_device_manual`，通过控制台生成。

DeviceManual 管理四页顺序，复用原版 MultiPageUIManager 的背景、关闭、翻页和页码。
每页是独立 ManualPage 子类，Build(RectTransform page) 负责本页布局。
当前四页均为空白，没有正文、插图或演示。未接入解锁。
