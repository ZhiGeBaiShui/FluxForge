"""Build the standalone story review from current dialogue constants and source files."""

import base64
import csv
import json
import re
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STORY = ROOT / 'src/Story'
SOURCES = {}
TEXTS = {}
PROVENANCE = {}
ARRAYS = {}
ZH = {row[0]: row[1].replace('\\n', '\n') for row in csv.reader((ROOT / 'mod/Localization/zh.csv').open(encoding='utf-8-sig')) if len(row) == 2}
STRING = r'"(?:\\.|[^"\\])*"'


def decode(value):
    return json.loads(value)


for path in sorted(STORY.rglob('*.cs')):
    if 'Runtime' in path.parts or 'Portraits' in path.parts:
        continue
    source = path.read_text(encoding='utf-8-sig')
    relative = path.relative_to(ROOT).as_posix()
    SOURCES[relative] = source
    for match in re.finditer(r'const\s+string\s+(\w+)\s*=\s*(' + STRING + r'(?:\s*\+\s*' + STRING + r')*)\s*;', source):
        key = path.stem + '.' + match[1]
        raw = ''.join(decode(s) for s in re.findall(STRING, match[2]))
        TEXTS[key] = ZH.get(raw, raw)
        PROVENANCE[key] = [relative, source[:match.start()].count('\n') + 1]
    for match in re.finditer(r'string\[\]\s+(Visitor\d+)\s*=\s*\{(.*?)\};', source, re.S):
        ARRAYS[match[1]] = [decode(s) for s in re.findall(STRING, match[2])]

for i in range(1, 5):
    name = 'Visitor' + str(i).zfill(2)
    ARRAYS[name] = [value for key, value in ZH.items() if key.startswith('story.HospitalVisitorTexts.' + name + '.')]

NODES = {}
CHAPTERS = []
USED = set()


def text_keys(group, names):
    keys = [group + 'Texts.' + name for name in names.split()]
    USED.update(keys)
    return [{'text': TEXTS[k], 'source': PROVENANCE[k]} for k in keys]


def label(group, name):
    key = group + 'Texts.' + name
    USED.add(key)
    return TEXTS[key]


def node(identifier, title, npc, group=None, names='', notes=None, source=None, system=False):
    lines = text_keys(group, names) if group else []
    NODES[identifier] = dict(id=identifier, title=title, npc=npc, lines=lines,
                             notes=notes or [], source=source or [], system=system, options=[])
    return identifier


def option(identifier, caption, *routes, kind='choice'):
    NODES[identifier]['options'].append(dict(label=caption, kind=kind,
        routes=[dict(condition=c, target=t, effect=e) for c, t, e in routes]))


def go(identifier, target, condition='无额外条件；按当前剧情顺序继续。', effect='', caption='继续', kind='flow'):
    option(identifier, caption, (condition, target, effect), kind=kind)


def chapter(title, npc, entry, trigger, source, priority=None):
    CHAPTERS.append(dict(title=title, npc=npc, entry=entry, trigger=trigger,
                         source=source, priority=priority))


F = 'FlowEmitterArrival'
fs = 'src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantStory.cs'
fa = 'src/Story/Npcs/FlowEmitterMerchant/Events/FlowEmitterArrival/FlowEmitterArrivalAssembly.cs'
fm = 'src/Story/Npcs/FlowEmitterMerchant/Events/FlowEmitterArrival/FlowEmitterArrivalMaterials.cs'
node('oya-start', '初访 · 流源工艺邀请', 'oya', F, 'Greeting1 Greeting2 Purpose1 Purpose2 Invitation1 Invitation2', source=[fs, fa])
node('oya-retry', '再次邀请体验', 'oya', F, 'RetryInvitation', source=[fs, fa])
chapter('01 / 流源工艺初体验', 'oya', 'oya-start', '奥雅每7天确定来访；入门剧情尚未完成。首次来访播放完整介绍，之后仍未完成时播放简短邀请。拒绝体验不会标记入门完成。', fs, 100)
option('oya-start', '查看其他到访情形', ('奥雅来访次数＞1，且入门剧情尚未完成。', 'oya-retry', '只改变开场，后续选项相同。'), kind='condition')
node('oya-kit', '接受体验 · 领取旧装置', 'oya', F, 'Gift1 Gift2 Gift3 Instructions1 Instructions2 Instructions3 Instructions4 Goal1 Goal2 Goal3', notes=['在 Gift3 后赠送：100 mL酸液（90%酸、10%化学污染）、旧发射器×2、旧升温装置×1、旧净化装置×1、入门便条。赠礼时机保留在此说明中。'], source=[fm, fa])
node('oya-decline', '暂不体验', 'oya', F, 'Decline DeclineFarewell1 DeclineFarewell2', notes=['结束本次来访；不写入入门完成标记，7天后仍可再次邀请。'], source=[fa])
for entry in ['oya-start', 'oya-retry']:
    option(entry, '有兴趣，我来试试。', ('玩家接受体验。', 'oya-kit', '赠送入门物品，进入操作与检查阶段。'))
    option(entry, '今天先不了。', ('玩家暂时拒绝。', 'oya-decline', '奥雅离开，本次不赠送入门物品。'))
node('oya-missing', '检查 · 没有找到酸试剂', 'oya', F, 'Missing1 Missing2', source=[fm])
node('oya-dilute', '检查 · 浓度不足', 'oya', F, 'TooDilute1 TooDilute2', source=[fm])
node('oya-passed', '检查 · 达到95%', 'oya', F, 'Success1 Success2 Success3', notes=['不收走玩家的试剂。'], source=[fm])
node('oya-giveup', '暂未成功 · 鼓励继续研究', 'oya', F, 'Encourage1 Encourage2 Encourage3')
node('oya-depart', '赠送其余旧装置并告别', 'oya', F, 'DepartureGift1 DepartureGift2 DepartureGift3 Farewell1 Farewell2', notes=['第三句后赠送旧转向装置×4、旧加速装置×1、旧降温装置×1；最后一句后标记入门剧情完成。即使选择“抱歉，我没弄出来。”也会进入此段并完成入门。'], source=['src/Story/Npcs/FlowEmitterMerchant/Events/FlowEmitterArrival/FlowEmitterArrivalDeparture.cs'])
for entry in ['oya-kit', 'oya-missing', 'oya-dilute']:
    option(entry, '你看这样对吗？（把酸试剂放在桌上）',
           ('后柜台存在 med_bottle_red，含酸且酸占比≥95%。', 'oya-passed', '检查通过，保留试剂。'),
           ('找到了含酸的 med_bottle_red，但没有任何一瓶达到95%。', 'oya-dilute', '仅提示，返回原操作选项。'),
           ('没有找到含酸的 med_bottle_red；其他瓶型不计入这次入门检查。', 'oya-missing', '仅提示，返回原操作选项。'))
    option(entry, '抱歉，我没弄出来。', ('玩家结束本次尝试。', 'oya-giveup', '材料与机器留给玩家，继续赠礼和告别。'))
go('oya-passed', 'oya-depart')
go('oya-giveup', 'oya-depart')
node('oya-second', '第二阶段到访 · 新装置与手册', 'oya', 'FlowEmitterSecondVisit', 'Greeting NewDevice ColdTrap ExitLine', notes=['在手册说明后赠送流源手册；整段正常结束后标记第二次剧情完成，并上架全新装置，进入原版商店。', '本次货单：冷阱×1、净化×1、发射器×2、转向×4、升温×1、降温×1、加速×2。'], source=['src/Story/Npcs/FlowEmitterMerchant/Events/FlowEmitterSecondVisit/FlowEmitterSecondVisitAssembly.cs', 'src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantStock.cs'])
chapter('02 / 奥雅第二阶段到访', 'oya', 'oya-second', '入门剧情已完成，第二阶段到访剧情尚未完成；事件只正常完成一次。每7天的奥雅来访规则不变。', fs, 90)
node('oya-shop', '装置商店', 'oya', notes=['接入原版商店交互，不新增剧情玩家选项。离店由原版处理。'])
go('oya-second', 'oya-shop', effect='正常完成剧情后上架第二访货单。', caption='进入商店')
node('oya-generic', '日常到访 · 补充装置', 'oya', 'FlowEmitterGenericVisit', 'Greeting MoreItems', notes=['正常结束时按剧情标记补货：入门完成可售发射器×2、净化×1；第二访完成增加冷阱×1、离心机×1；另有 ThirdVisitKey 控制加速×2、转向×2、直管×4，本页不把它推导成不存在的第三访剧情。'], source=['src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantStock.cs'])
chapter('03 / 奥雅日常售卖', 'oya', 'oya-generic', '每7天到访；没有命中优先级更高的入门或第二阶段剧情时。', fs, 10)
go('oya-generic', 'oya-shop', effect='刷新日常货物。', caption='进入商店')

A = 'AcidBuyerArrival'
R = 'AcidBuyerRepeatVisit'
H = 'AcidBuyerFirstHighPurity'
vs = 'src/Story/Npcs/AcidBuyer/AcidBuyerStory.cs'
vt = 'src/Story/Npcs/AcidBuyer/AcidBuyerTrade.cs'
session = 'src/Story/Npcs/AcidBuyer/AcidBuyerTradeSession.cs'
node('vera-first', '初次见面 · 收购酸和碱', 'vera', A, 'Line1 Line2 Line3 Line4 PriceIntro PriceStep PricePure', notes=['第三句后赠送受污染酸液×2、碱液×2，每瓶250 mL。正常结束整段初访后才记录已经见面。'], source=[vs, 'src/Story/Npcs/AcidBuyer/AcidBuyerStock.cs'])
chapter('04 / 薇拉首次到访', 'vera', 'vera-first', '奥雅入门剧情完成后，薇拉可每7天确定来访；尚未正常完成薇拉初访。若同时命中更高优先级医院事件，先由医院事件接管。', vs, 100)
node('vera-repeat', '日常收购 · 再次到访', 'vera', R, 'Line1 Line2', source=[vs])
chapter('05 / 薇拉日常收购', 'vera', 'vera-repeat', '薇拉每7天来访的兜底事件；未命中合同提议、合同送料、取货、里程碑或终止事件。', vs, 10)

for prefix, group, start in [('first', A, 'vera-first'), ('repeat', R, 'vera-repeat')]:
    initial = 'v-'+prefix+'-root'
    success = 'v-'+prefix+'-success'
    high = 'v-'+prefix+'-high'
    node(initial, '准备卖货', 'vera', notes=['这是玩家选择节点，不额外增加游戏台词。'], source=[vt])
    go(start, initial)
    node(success, '收货后继续询问', 'vera', group, 'Success ContinueQuestion', notes=['成功交易已收走合格整瓶并付款；不会收走发射器等流体装置。'], source=[vt])
    node(high, '首次高浓度交付 · 惊讶', 'vera', group, 'Success', source=[vt, session])
    NODES[high]['lines'] += text_keys(H, 'Inspection Question')
    for suffix, response in [('self', 'SelfMadeResponse'), ('luck', 'LuckResponse')]:
        target = 'v-'+prefix+'-'+suffix
        node(target, '高浓度问答 · 回答', 'vera', H, response+' ContinueQuestion', notes=['写入高浓度问答完成标记，排队下次合同提议；返回本次卖货选项。'], source=['src/Story/Npcs/AcidBuyer/Events/AcidBuyerFirstHighPurity/Edges/AcidBuyerFirstHighPurityEdges.cs'])
        go(target, success+'-menu')
        option(high, label(H, 'SelfMadeOption' if suffix=='self' else 'LuckOption'), ('玩家选择该回答。', target, '两个回答均解锁后续合同提议。'))
    node(success+'-menu', '继续卖货', 'vera', notes=['承接薇拉刚刚说出的“还有吗？”，这里只显示玩家选项。'])
    for state in [initial, success+'-menu']:
        # Failure returns to the SAME menu (pre-sale vs post-sale), not another visit.
        no = state+'-no'; low = state+'-low'
        node(no, '桌面检查 · 没有目标试剂', 'vera', group, 'NoAcidOrBase', source=[vt])
        node(low, '桌面检查 · 浓度不够', 'vera', group, 'ConcentrationTooLow', source=[vt])
        go(no, state, '检查失败；没有收货或付款。', caption='返回原选择')
        go(low, state, '没有任何合格酸/碱瓶；没有收货或付款。', caption='返回原选择')
        option(state, label(group, 'TradeOption'),
            ('后柜台没有含酸或碱的可收液体容器。', no, '不交易。'),
            ('存在酸或碱，但所有候选瓶的目标浓度都低于90%。', low, '不交易。'),
            ('至少一瓶酸或碱浓度≥90%；本次累计出现≥99%交付，且从未完成高浓度问答。', high, '收合格瓶并付款，首次惊讶问答只发生一次；不要求达到100 mL。'),
            ('至少一瓶酸或碱浓度≥90%，但不满足首次高浓度问答条件。', success, '收合格瓶并付款；不合格瓶留在桌面。'))
    go(success, success+'-menu')
    exit_id = 'v-'+prefix+'-exit'
    node(exit_id, '未交货 · 告别', 'vera', group, 'ExitLine')
    option(initial, label(group, 'ExitOption'), ('玩家尚未成交，选择结束本次收购。', exit_id, '薇拉离开。'))
    for suffix, name in [('enough', 'SoldOutHigh'), ('few', 'SoldOutLow')]:
        node('v-'+prefix+'-'+suffix, '收购结束 · 告别', 'vera', group, name, source=[session])
    option(success+'-menu', label(group, 'SoldOutOption'),
           ('本次到访所有批次中，≥99%浓度货物累计体积≥100 mL。', 'v-'+prefix+'-enough', '按整次到访累计判断；后续交低浓度货不会清空已累计体积。'),
           ('本次到访所有批次中，≥99%浓度货物累计体积＜100 mL。', 'v-'+prefix+'-few', '薇拉离开。'))

O = 'HospitalContractOffer'
offer_source = 'src/Story/Hospital/Events/HospitalContractOffer/HospitalContractOfferEdges.cs'
node('offer', '医院合同 · 首次提议', 'vera', O, 'Greeting1 Greeting2 Greeting3 Greeting4', source=[vs, offer_source])
chapter('06 / 医院合同提议', 'vera', 'offer', '高浓度问答已完成并排队合同提议；尚未签约，且没有永久拒绝。下次符合来访条件时触发。优先级150。', vs, 150)
node('offer-repeat', '医院合同 · 再次询问', 'vera', O, 'RepeatGreeting1 RepeatGreeting2', notes=['“怎么供。”是否出现取决于全存档 DetailsExplained；不是每次到访重置。'], source=[offer_source])
chapter('07 / 再次询问合同', 'vera', 'offer-repeat', '先前暂时只散卖，合同提议仍排队；下次7天来访。已正常结束过合同提议，或曾解释过合同详情。', offer_source)
node('offer-new-menu', '首次合同选项', 'vera')
node('offer-repeat-menu', '再次合同选项 · 尚未问过详情', 'vera')
node('offer-after-menu', '再次合同选项 · 已问过详情', 'vera')
go('offer', 'offer-new-menu')
option('offer-repeat', '查看本次可用选项', ('从未询问过“怎么供。”。', 'offer-repeat-menu', '第一项仍为“怎么供。”。'), ('此前任意一次来访已经询问过详情。', 'offer-after-menu', '第一项改为“行，我答应了。”，不再重复询问。'), kind='condition')
node('offer-ask', '说明合同 · 交付需求清单', 'vera', O, 'AskLine1 AskLine2 AskLine3 AskLine4', notes=['第一句结束后赠送需求清单，并永久记住已解释详情。'], source=[offer_source])
node('offer-ask-repeat', '再次提议时首次了解合同', 'vera', O, 'AskLine1 AskLine2 AskLine3 AskLine4', notes=['之前未问过详情，本次第一次解释；赠送需求清单并永久记住。后续暂缓签约选项沿用“还是只散卖。”。'], source=[offer_source])
node('offer-ask-menu', '听完说明后的合同选项', 'vera')
go('offer-ask', 'offer-ask-menu')
go('offer-ask-repeat', 'offer-after-menu')
node('offer-accept', '签订合同', 'vera', O, 'AcceptLine1 AcceptLine2 AcceptLine3', notes=['立即登记签约日与医院阵营；签约日+3首次送料、+7首次取货，此后固定对应星期，跳天不在其他星期补发。'], source=['src/Story/Hospital/HospitalContractState.cs'])
node('offer-decline', '暂时只散卖 · 回到收购', 'vera', O, 'DeclineLine ResumeTrade', notes=['不永久拒绝，下次来访可以重新询问合同。'])
node('offer-no', '没有意愿 · 回到收购', 'vera', O, 'NoInterestLine1 NoInterestLine2 ResumeTrade', notes=['永久拒绝：清除提议排队标记，之后不再提议合同；仍可日常散卖。'], source=[offer_source])
go('offer-decline', 'v-repeat-root')
go('offer-no', 'v-repeat-root')
for entry in ['offer-new-menu', 'offer-repeat-menu', 'offer-after-menu', 'offer-ask-menu']:
    if entry in ['offer-new-menu', 'offer-repeat-menu']:
        option(entry, label(O, 'AskOption'), ('本存档尚未解释过合同详情。', 'offer-ask-repeat' if entry=='offer-repeat-menu' else 'offer-ask', '交付需求清单并标记已经解释。'))
    else:
        option(entry, label(O, 'AcceptOption'), ('合同详情已经解释，玩家同意签约。', 'offer-accept', '开始合同与医院声望。'))
    option(entry, label(O, 'DeclineOption' if entry in ['offer-new-menu', 'offer-ask-menu'] else 'RepeatDeclineOption'), ('玩家暂缓签约。', 'offer-decline', '回到日常卖货，保留后续提议。'))
    option(entry, label(O, 'NoInterestOption'), ('玩家明确没有意愿。', 'offer-no', '永久拒绝合同，但继续本次日常收购。'))

node('contract-paper', '医院采购清单', 'vera', notes=[TEXTS['HospitalContractOfferEdges.PriceBody']], source=[offer_source])
option('offer-ask', '阅读收到的需求清单', ('游戏中在“给，这是需求清单。”之后赠送的便条内容。', 'contract-paper', '审阅辅助入口，不是游戏玩家选项。'), kind='reference')
option('offer-ask-repeat', '阅读收到的需求清单', ('本次首次了解详情时赠送的便条内容。', 'contract-paper', '审阅辅助入口，不是游戏玩家选项。'), kind='reference')

P = 'HospitalContractPickup'
ps = 'src/Story/Hospital/Events/HospitalContractPickup/HospitalContractPickupEdges.cs'
node('pickup', '每周收货', 'vera', P, 'Greeting', notes=['收货日固定为签约星期；最高需求首次邀请会替换此问候。原液任务进行时另插入任务询问。'], source=[ps])
chapter('08 / 医院每周收货', 'vera', 'pickup', '合同有效且到达约定收货星期；签约后第7天首次收货。跳天不会改成任意日期补收。', 'src/Story/Hospital/HospitalContractSchedule.cs', 200)
node('pickup-menu', '本次提交菜单', 'vera', notes=['普通提交与原液任务提交共享本次累计分数；任务进行时共三个选项，完成后只保留普通提交、结束。'])
go('pickup', 'pickup-menu')
node('pickup-low', '浓度不足 · 返回', 'vera', P, 'UnderConcentration')
node('pickup-empty', '没有目标试剂 · 返回', 'vera', P, 'ZeroInspection')
node('pickup-taken', '收货计分 · 可以继续', 'vera', P, 'Continue', notes=['立即收走合格货物并提示收货分数x/300；此时不付款。'])
for key in ['pickup-low','pickup-empty','pickup-taken']: go(key,'pickup-menu')
option('pickup-menu', label(P,'ConfirmOption'),
       ('桌面有低浓度候选试剂。','pickup-low','本批不收货、不计分。'),
       ('没有合格试剂。','pickup-empty','返回菜单。'),
       ('有合格试剂，且没有低浓度候选瓶。','pickup-taken','收货、累积分数；进行中的任务原液不经此选项接收。'))
node('pickup-short','不足交付 · 二次确认','vera',P,'ShortQuestion')
node('pickup-zero','零交付 · 二次确认','vera',P,'ZeroInspection ZeroQuestion')
node('settle','本次统一结算','vera',P,'Total',notes=['只结算已提交货物，不重新扫描桌面。最高需求当次完成时该总分提示及普通评价被任务完成片段覆盖。'],source=['src/Story/Hospital/Events/HospitalContractPickup/Services/HospitalPickupSession.cs'])
option('pickup-menu',label(P,'FinishOption'),
       ('声望≥100且累计分数不足300。','settle','直接结束，不减声望，不二次确认。'),
       ('声望＜100且本次未交货。','pickup-zero','询问确认。'),
       ('声望＜100且累计分数不足300，有交货。','pickup-short','询问确认。'),
       ('累计分数≥300。','settle','统一付款与调整声望。'))
for key,retry in [('pickup-short','RetryOption'),('pickup-zero','ZeroRetryOption')]:
    option(key,label(P,'AcceptShortOption'),('确认结束。','settle','保留已提交分数并统一付款。'))
    option(key,label(P,retry),('继续交货。','pickup-menu','不结算。'))
node('pickup-evaluation','普通交货评价','vera',notes=['普通交货达到600分播超额感谢；达标播达标评价；不足播不足评价。只交任务原液时不追加普通评价。最高需求当次完成时覆盖普通评价。'])
for key,title,names in [('settle-met','达标','Met1'),('settle-over','超额','OverThanks Over3'),('settle-short','不足','ShortSettled'),('settle-zero','无交付','ZeroSettled')]:
    node(key,title,'vera',P,names);go(key,'pickup-insert')
node('pickup-insert','收货后专项任务插入','vera')
node('pickup-bye','本周收货结束','vera',P,'Farewell')
node('pickup-busy','外聘专家 · 本周忙碌','vera',P,'Busy1 Busy2')
option('settle','结算后的评价',('最高需求本次完成。','demand-complete','任务完成片段覆盖普通评价。'),('声望≥100且交付不足。','pickup-busy','不减声望，使用忙碌告别。'),('其他情况。','pickup-evaluation','正常结算评价。'),kind='condition')
option('pickup-evaluation','查看评价',('没有交货。','settle-zero',''),('普通货物且得分＜300。','settle-short',''),('普通货物且300≤分数＜600。','settle-met',''),('普通货物且分数≥600。','settle-over',''),('只有原液任务提交。','pickup-insert','不重复评价。'),kind='condition')
node('material','每周送料','vera','HospitalContractMaterial','Greeting',notes=['签约后第3天首送，以后固定该星期；发放时声望＜20为2瓶、20至39为3瓶、≥40为4瓶，每瓶250mL。100任务完成后额外赠送450mL瓶，含400mL有效塞纳普斯。'])
chapter('09 / 医院每周送料','vera','material','合同有效且到达送料星期。','src/Story/Hospital/HospitalContractSchedule.cs',190)
node('material-end','送料结束','vera',notes=['里程碑只在送料时插入，不抢占其他来访。'])
M='HospitalContractMilestone'
for tier,names in [(20,'At20_1 At20_2 At20_3 At20_4'),(40,'At40_1 At40_2')]:
    node('rep'+str(tier),str(tier)+'声望原料额度提示','vera',M,names)
    chapter(str(tier)+'声望额度提示','vera','rep'+str(tier),'只在正常送料的 MaterialDelivered 插入点提示一次，不单独占用收货日。',vs)
    go('rep'+str(tier),'material-end')
option('material','送料后额度提示',('40声望提示待播放。','rep40','按当前声望发料。'),('20声望提示待播放。','rep20','按当前声望发料。'),('没有待播放提示。','material-end',''),kind='condition')
D='HospitalHighestDemand'
ds='src/Story/Hospital/Events/HospitalHighestDemand/HospitalHighestDemand.cs'
node('demand','60声望 · 最高需求邀请','vera',D,'Greeting Offer1 Offer2 Offer3 Offer4')
chapter('12 / 60声望最高需求','vera','demand','声望≥60且从未接受/拒绝，在医院收货日替换普通问候；不在送料日邀请。',ds)
node('demand-accept','接受请求','vera',D,'AcceptResponse1 AcceptResponse2')
node('demand-decline','拒绝请求','vera',D,'DeclineResponse')
option('demand',label(D,'AcceptOption'),('玩家接受。','demand-accept','开启酸、碱、酒精各1000mL的100%纯度累计任务。'))
option('demand',label(D,'DeclineOption'),('玩家拒绝。','demand-decline','关闭此任务，普通合同继续。'))
go('demand-accept','pickup-menu');go('demand-decline','pickup-menu')
node('demand-progress','最高需求进度','vera',D,'Progress',notes=['变量为累计纯酸、纯碱、纯酒精体积。'])
node('demand-complete','最高需求完成','vera',D,'Complete',notes=['没有独立奖励；解锁100声望主线的前置。覆盖本次普通总结，告别只执行一次。'])
option('pickup-insert','检查最高需求',('任务进行中。','demand-progress',''),('任务未开始或已结束。','pickup-bye',''),kind='condition')
option('demand-progress','检查累计体积',('三种分别达到1000mL。','demand-complete','标记完成。'),('仍有不足。','pickup-bye','保留进度。'),kind='condition')
go('demand-complete','pickup-bye')
S='HospitalSynaprest'
ss='src/Story/Hospital/Events/HospitalSynaprest/Services/HospitalSynaprestState.cs'
node('syn-offer','100声望 · 原液提纯请求','vera',S,'Greeting1 Greeting2 Greeting3 Greeting4 Greeting5 Greeting6')
chapter('14 / 医院最终主线','vera','syn-offer','60声望任务已完成、医院声望≥100、从未接受/拒绝；在非送料且非收货的空闲日到访。',ss,270)
node('syn-accept','接受 · 赠送报废原料','vera',S,'Accept1 Accept2 Accept3 Accept4 Accept5 Accept6',notes=['750mL瓶：有效成分600mL，加6061µL淀粉与酸/碱之一，约99%。'])
node('syn-decline','拒绝 · 永久关闭原液任务','vera',S,'Decline')
option('syn-offer',label(S,'AcceptOption'),('玩家接受。','syn-accept','赠料并开始任务，日常合同继续。'))
option('syn-offer',label(S,'DeclineOption'),('玩家拒绝。','syn-decline','薇拉离开，日常合同继续。'))
node('syn-ask','交货日顺便询问','vera',S,'Ask1 Ask2');go('syn-accept','syn-ask',condition='后续医院收货日，原液任务进行中。');go('syn-ask','pickup-menu')
option('pickup-menu',label(S,'SubmitOption'),('原液任务进行中，桌面为空。','syn-empty','不收货、不计分。'),('原液任务进行中，没有塞纳普斯。','syn-missing','不收货、不计分。'),('原液任务进行中，只有不纯原液。','syn-impure','不收货、不计分。'),('有100%原液，首次成功提交。','syn-first','只收纯原液，每mL计5分。'),('有100%原液，之前已经提交过。','syn-later','累计有效成分和本次收货分数。'))
for key,field in [('empty','Empty'),('missing','Missing'),('impure','Impure')]:
    node('syn-'+key,'原液检查失败','vera',S,field);go('syn-'+key,'pickup-menu')
node('syn-first','首次交付惊喜','vera',S,'First1 First2 First3')
node('syn-once','一次性完成','vera',S,'AllAtOnce');go('syn-once','syn-complete')
node('syn-first-progress','首次部分交付','vera',S,'FirstProgress');go('syn-first-progress','pickup-menu')
option('syn-first','检查任务进度',('首次就累计达到550mL。','syn-once',''),('累计不足550mL。','syn-first-progress',''),kind='condition')
node('syn-later','后续交付进度','vera',S,'Progress')
option('syn-later','检查任务进度',('累计达到550mL。','syn-complete',''),('累计不足550mL。','pickup-menu','继续提交或结束。'),kind='condition')
node('syn-complete','医院最终任务完成','vera',S,'Complete1 Complete2 Complete3 CompleteContinue',notes=['进度不封顶。立即移除任务提交选项；普通提交开始接收100%塞纳普斯。每周正常送料额外赠送400mL有效成分原料，浓度>95%。没有接取后的放弃选项。黑针配方与合成路线留给奥雅后续扩展，尚未实现。'])
go('syn-complete','pickup-menu')

node('termination', '医院终止合同', 'vera', 'HospitalContractTermination', 'Black Gray', system=True, notes=['立即标记合同终止；不再执行医院合同送料/取货。此处两句由系统显示，不是薇拉对白。“特殊客人”只有这段提示，不能据此推导出新的已实现任务线。'], source=['src/Story/Hospital/Events/HospitalContractTermination/HospitalContractTerminationEdges.cs',vs])
chapter('14 / 合同终止', 'vera', 'termination', '合同已开始、尚未终止，医院声望≤-20；优先级300，高于里程碑和日常合同事件。只正常完成一次。', vs, 300)

proc='src/Story/Hospital/Procurement/Services/HospitalProcurementClients.cs'
for i in range(1,5):
    key='visitor'+str(i)
    node(key, '80声望 · 中心医院访客 '+str(i).zfill(2), key, notes=['原版顾客生成阶段，已解锁“特殊采购”时每日10%概率追加一名，从4位访客中随机选择。不是每位各10%。', '当前源码采购标签是 ALL_TYPE（不限医药品）；预算=随机250–300＋当前天数×2。接受违禁品，启用医院perk时证据价值采用整数除2；保留上层区化学纯度鉴别。', '招呼后接入原版 AddBasicDialog 与普通买卖流程；本页只逐字收录项目定义的招呼，不编写原版通用交易对白。'], source=[proc,'src/Story/Hospital/Procurement/HospitalProcurement.cs','src/Story/Hospital/Procurement/Patches/HospitalCrimeModifierPatch.cs'])
    NODES[key]['lines']=[{'text':v,'source':[proc,1]} for v in ARRAYS['Visitor'+str(i).zfill(2)]]
    chapter(str(14+i)+' / 医院采购访客 '+str(i).zfill(2), key, key, '医院80声望“特殊采购”已解锁；每日顾客生成时整体10%概率追加一位，随机挑选4个已注册身份之一。', proc)
    shop=key+'-shop'
    node(shop, '医院访客 · 原版采购交易', key, notes=['进入原版顾客采购交互。这里没有项目新增的剧情选择；买卖、离场使用原版流程。'], source=[proc])
    go(key, shop, caption='进入采购交易')

# Shared rules are visible independently of a particular branch.
RULES=[
 dict(title='来访调度与剧情优先级',body='未签约时普通来访7天冷却；有效合同期间按固定送料/收货星期来访。20/40额度提示是送料插入。100声望请求需要60任务完成且当天无送料、收货。',source=[vs,fs]),
 dict(title='散卖计价与高浓度累计',body='整瓶浓度≥90%的酸/碱才收。基础价格为每10 mL 1信用点（每瓶向下取整）；90%至99%基础倍率1，99%以上每提高0.1个百分点倍率+0.1，100%倍率3。高浓度问答只需本次出现≥99%交付且从未问答；告别好评另要求本次各批≥99%货物累计≥100 mL。',source=[vt,session]),
 dict(title='医院合同计分、货款和声望',body='酸/碱基础分1，酒精0.8；最低浓度90%；得分=整瓶mL×品类基础分×浓度权重。90%至99%权重1，99%以上每0.1个百分点+0.1，100%权重3。周目标300分，600分触发超额感谢。基础货款=floor(得分×0.1)；结算前声望≥60时再floor(基础货款×1.2)。达标声望=min(floor(得分/100),20)；不足为-ceil((300-得分)/100)，零交付-3；声望≥100且不足时不减声望。所有提交分数在结束时统一付款。',source=['src/Story/Hospital/HospitalContractScore.cs','src/Story/Hospital/HospitalContractTrade.cs']),
 dict(title='声望奖励',body='20：原料3瓶；40：4瓶；60：周货款1.2倍；80：医院采购访客每日10%机会；100：外聘专家，不足或零交付不减声望。医院550mL原液提纯主线已实现；奥雅黑针配方与原液合成路线待后续设计。',source=['src/Story/Hospital/HospitalReputation.cs','src/Story/Hospital/HospitalReputationTexts.cs']),
 dict(title='审阅页的条件不是玩家选项',body='页面中“玩家选项”来自游戏剧情；“条件分支”“继续”“阅读清单”等为审阅导航。点击选项只展开它可能出现的条件，再点击条件进入目标剧情。本页不模拟真实存档、桌面物品或付款，返回不会重复发放物品。台词保留源码原文，包括标点与变量；源码中的系统颜色标记仅用于文本展示。',source=[])
]

for n in NODES.values():
    for o in n['options']:
        assert o['routes']
        for r in o['routes']:
            assert r['target'] in NODES,(n['id'],r['target'])

portraits={}
for key,name in [('oya','npc_saleswoman'),('vera','npc_acid_buyer')]+[('visitor'+str(i),'npc_hospital_visitor_'+str(i).zfill(2)) for i in range(1,5)]:
    portraits[key]='data:image/png;base64,'+base64.b64encode((ROOT/'mod/Textures'/ (name+'.png')).read_bytes()).decode()

DATA=dict(nodes=NODES,chapters=CHAPTERS,rules=RULES,portraits=portraits,sources=SOURCES,
          textInventory=[dict(key=k,text=v,source=PROVENANCE[k],inGraph=k in USED) for k,v in TEXTS.items() if k.split('.')[0].endswith('Texts') and not k.endswith('PerkId')],
          built=datetime.now().strftime('%Y-%m-%d %H:%M'),stats=dict(nodes=len(NODES),chapters=len(CHAPTERS),sourceFiles=len(SOURCES)))
template=(ROOT/'tools/story-review-template.html').read_text(encoding='utf-8')
payload=json.dumps(DATA,ensure_ascii=False,separators=(',',':')).replace('</',r'<\/')
output=ROOT/'dist/FluxForge-story-review.html'
output.write_text(template.replace('/*__STORY_DATA__*/',payload),encoding='utf-8')
print(str(output))
print(json.dumps(DATA['stats']))
