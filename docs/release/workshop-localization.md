# 工坊双语自动发布

当前无需分别粘贴中英文。`workshop-0.2.0.vdf`包含完整english、schinese标题与描述，以及共享tags。

## 执行

保持桌面Steam客户端登录工坊作者账号，双击`publish-bilingual.cmd`。脚本读取这份VDF，使用游戏自带Steamworks.NET和steam_api64.dll，依次调用官方SetItemUpdateLanguage、SetItemTitle、SetItemDescription、SetItemTags、SubmitItemUpdate，然后按语言回读标题、描述和标签验证。

它只更新已有条目3840844438的双语元数据与标签，不创建新条目，不重新上传Mod文件，不修改封面或可见性。不收集密码、验证码或浏览器cookie。用户仍可用原SteamCMD命令单独更新Mod内容；原生SteamCMD只会使用VDF顶层的英文标题与描述。

## 为什么需要本地发布器

已检查本机`E:/code/steam/steamconsole.dll`的workshop_build_item解析路径（0x100601be–0x100607a3）。实际读取appid、publishedfileid、filetype、title、description、visibility、previewfile、contentfolder、kvtags、changenote，没有读取language/localization或普通tags块。

因此localizations和tags是本地发布器支持的VDF扩展，不声称原SteamCMD能够读取。此前“给SteamCMD添加tags即可设置标签”没有经过验证；此版本改为显式调用SetItemTags。

默认英文标题仍在VDF顶层，以保持SteamCMD内容上传兼容。中英文正文都保留QQ群和真正GIF动画链接。

## 本轮验证范围

- 发布器构建通过，0警告、0错误；本地读取两个语言块成功。
- 主Mod构建通过，0警告、0错误。
- 没有调用--publish，未修改工坊线上内容。
- 只读Steam查询已初始化客户端，但查询接口返回k_EResultFail，尚未完成线上回读验证。该返回值没有更具体原因，不能声称线上发布链路实测通过。

若发布或回读失败，脚本返回非零退出码，显示实际失败信息，不自动重试。两个语言提交分别由Steam处理，不增加回滚或补偿逻辑。

## 源码依据

- `SteamAPI.InitEx`返回枚举与错误文本：必须处理客户端/接口初始化失败。
- SetItem*返回bool；SubmitItemUpdateResult_t包含EResult和需要接受工坊协议的标志；发布器逐项检查，不替用户接受协议。
- CallResult<T>回调明确带bIOFailure；查询回调有EResult；查询句柄由ReleaseQueryUGCRequest释放，客户端由Shutdown释放。
- 等待回调上限120秒，超时明确报告结果未知，提示先查远端，不自动重复提交。
- VDF解析器只支持本项目采用的引号键值与嵌套块，语法不符直接报错，不静默补默认语言。

官方接口：https://partner.steamgames.com/doc/api/ISteamUGC#SetItemUpdateLanguage

## 动图

原扩展名为.gif的文件实际是WebP。已另存真正GIF：267×192、66帧。新直链返回image/gif，下载内容与转换后的源GIF逐字节一致。尚未在Steam正文中确认显示：
https://i.ibb.co/Kc5mrRDk/liquid-loop-demo-steam.gif

## 2026-11-17 NoConnection 实际原因

用户执行发布后收到SubmitItemUpdate: k_EResultNoConnection。只读取证：桌面Steam的logs/connection_log.txt在11:08:20记录Session Replaced、Logged Off、not auto reconnecting due to Session Replaced；SteamCMD的connection_log.txt同秒登录成功，且SteamCMD进程仍在运行。

再次仅运行--verify，SteamAPI.InitEx成功，但SteamUser.BLoggedOn()明确返回False。可确认本地Steam API初始化不代表桌面Steam已连接服务器。

发布器已在提交/查询前读取BLoggedOn并打印连接状态；断开时直接提示退出SteamCMD并恢复桌面Steam联机。该判断有实际日志和实时API证据，不增加自动重连、反复提交或回滚逻辑。

操作顺序：SteamCMD内输入quit → 在桌面Steam恢复账号登录/联机 → 运行publish-bilingual.cmd。内容上传用SteamCMD完成后，也要先退出该会话再用依赖桌面Steam的双语发布器。本轮没有调用--publish，未更新工坊内容。

官方连接状态定义：https://partner.steamgames.com/doc/api/ISteamUser#BLoggedOn 。日志中的[API loaded no]单行不是这次断线的判据，实际依据为Session Replaced和BLoggedOn=False。
