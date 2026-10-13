# -*- coding: utf-8 -*-
"""自检：标注声明的路径集 是否严格等于 git 实际改动集（在范围内的部分）。

用途：GATE-DELTA 的前置核查。gate-runner 做后缀匹配，本脚本做精确匹配，
两者互补——精确匹配能发现「后缀巧合掩盖的路径写错」。
"""
import subprocess
import re
import os
import sys

REPO = r'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统'
ANN = os.path.join(REPO, r'.codebuddy\tmp\econ-frontend-annotation.md')
OOS = os.path.join(REPO, r'.codebuddy\tmp\econ-out-of-scope.txt')

BS = chr(92)   # backslash
SL = '/'


def git_changed():
    def run(args):
        r = subprocess.run(['git', '-c', 'core.quotePath=false'] + args,
                           cwd=REPO, capture_output=True)
        return [x.strip().strip('"') for x in
                r.stdout.decode('utf-8', 'replace').splitlines() if x.strip()]
    files = []
    files += run(['diff', '--name-only', 'HEAD'])
    files += run(['ls-files', '--others', '--exclude-standard'])
    return files


def norm(p):
    return p.replace(BS, SL).strip()


def parse_annotation():
    raw = open(ANN, encoding='utf-8').read()
    m = re.match(r'(?s)^---\s*\n(.*?)\n---\s*\n', raw)
    if not m:
        print('!! 标注 front matter 未闭合或缺失 —— 请检查首尾 --- 是否成对')
        sys.exit(1)
    body = m.group(1)
    paths = []
    for line in body.splitlines():
        mm = re.match(r'^\s*-\s*path:\s*(.+?)\s*$', line)
        if mm:
            paths.append(norm(mm.group(1).strip().strip('"').strip("'")))
    return paths


def parse_oos():
    res = []
    for line in open(OOS, encoding='utf-8').read().splitlines():
        t = line.strip()
        if t and not t.startswith('#'):
            res.append(norm(t))
    return res


def main():
    changed_all = set(norm(p) for p in git_changed())
    ann = parse_annotation()
    oos = parse_oos()

    # 与 gate-runner.ps1 ShouldSkip 对齐：生成物/日志类不参与比对。
    # ⚠ 按扩展名 + 目录段判定，不用子串（否则 ArchiveMainTabWindow.Cache.cs 被误伤）。
    skip_ext = {'.log', '.cache', '.dll', '.pdb', '.bak'}
    skip_dir_seg = {'obj', 'bin', '_backups', '_backup', '.vs'}

    def should_skip(p):
        segs = [s.lower() for s in p.split('/')]
        if any(s in skip_dir_seg for s in segs):
            return True
        return any(segs[-1].endswith(e) for e in skip_ext)

    changed = set(p for p in changed_all if not should_skip(p))
    skipped = len(changed_all) - len(changed)

    ann_set = set(ann)
    oos_set = set(oos)

    print('git 改动集(原始)  : %d' % len(changed_all))
    print('git 改动集(参与)  : %d  (过滤生成物 %d)' % (len(changed), skipped))
    print('标注声明          : %d (去重 %d)' % (len(ann), len(ann_set)))
    print('范围外清单        : %d' % len(oos_set))

    dup = [p for p in ann_set if ann.count(p) > 1]
    if dup:
        print('!! 标注内重复路径: %d' % len(dup))
        for d in dup:
            print('   - ' + d)

    # 1) 标注里声明的文件，git 是否真的改了
    phantom = sorted(p for p in ann_set if p not in changed)
    print('')
    print('[1] 标注有 / git 无 (phantom): %d' % len(phantom))
    for p in phantom:
        print('   - ' + p)

    # 2) git 改了、但既不在标注也不在范围外清单
    uncovered = sorted(p for p in changed if p not in ann_set and p not in oos_set)
    print('')
    print('[2] git 有 / 标注与范围外清单都无 (uncovered): %d' % len(uncovered))
    for p in uncovered:
        print('   - ' + p)

    # 3) 范围外清单里混入了标注内文件（严重：会掩盖遗漏）
    overlap = sorted(p for p in oos_set if p in ann_set)
    print('')
    print('[3] 同时在范围外清单与标注内 (overlap, 应为 0): %d' % len(overlap))
    for p in overlap:
        print('   - ' + p)

    # 4) 范围外清单里写错路径（不存在于改动集）
    oos_stale = sorted(p for p in oos_set if p not in changed)
    print('')
    print('[4] 范围外清单里 git 无对应改动 (stale): %d' % len(oos_stale))
    for p in oos_stale[:20]:
        print('   - ' + p)

    ok = (not phantom) and (not uncovered) and (not overlap)
    print('')
    print('结论: %s' % ('PASS —— 标注集 == git 改动集（范围内）' if ok else 'FAIL —— 见上方各节'))


if __name__ == '__main__':
    main()
