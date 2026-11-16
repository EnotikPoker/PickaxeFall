#!/usr/bin/env python3
"""Compare two built native Balance Lab players on the same 480 runs (default).
Each player must have its matching manifest.json. No player profiles are touched.
"""
import argparse
import concurrent.futures
import csv
import html
import json
import math
from pathlib import Path
import queue
import statistics
import subprocess
import threading
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--before', required=True, type=Path)
    parser.add_argument('--after', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--samples', default=8, type=int)
    parser.add_argument('--seconds', default=1800, type=int)
    parser.add_argument('--workers', default=4, type=int)
    args = parser.parse_args()
    if not 1 <= args.samples <= 128 or not 60 <= args.seconds <= 1800 or args.workers not in (2, 4, 6, 8):
        parser.error('samples 1..128, seconds 60..1800, workers 2/4/6/8')
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=False)
    started = time.monotonic()
    deadline = started + args.seconds
    lock = threading.Lock()
    rows = []
    errors = []
    paths = {'before': args.before.resolve(), 'after': args.after.resolve()}
    manifests = {v: json.loads((p.parent / 'manifest.json').read_text()) for v, p in paths.items()}
    maximum = {}
    for price in manifests['after']['prices']:
        maximum[price['key']] = max(maximum.get(price['key'], 0), price['level'])
    counts = len(maximum)
    jobs = []
    for seed_index in range(args.samples):
        seed = 8127 + seed_index * 104729
        for map_id in range(3):
            for tier in range(5):
                for stage in range(2):
                    for active in (False, True):
                        levels = [0 if stage == 0 else max(1, math.floor(maximum[k] * .25)) for k in range(counts)]
                        scenario = dict(id=f'patch-m{map_id}-t{tier}-s{stage}-a{int(active)}', map=map_id,
                                        tier=tier, point=stage, stage=stage, active=active, levels=levels,
                                        cohort='patch', mask=0, sampleCount=args.samples)
                        jobs.append(dict(scenario=scenario, seed=seed, limit=600, diagnostic=False))
    queues = {}
    for version in paths:
        q = queue.Queue()
        for job in jobs:
            q.put(job)
        queues[version] = q
    total = len(jobs) * 2
    (out / 'provenance.json').write_text(json.dumps(dict(before=manifests['before'], after=manifests['after'],
        samples=args.samples, total=total, seconds=args.seconds, workers=args.workers, seed=8127,
        trialPolicy='4 taps/s; start briefing automatically; includes trial and result time',
        upgrades='none / 25% of each skill; weighted team/rescue branches'), ensure_ascii=False, indent=2))

    def worker(version, index):
        response = out / f'{version}-{index}.json'
        logfile = out / f'{version}-{index}.log'
        command = [str(paths[version]), '-batchmode', '-nographics', '-noaudio', '--balance-worker',
                   '--response=' + str(response), '--cancel=' + str(out / 'cancel'), '-logFile', str(logfile)]
        process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, text=True)
        try:
            while time.monotonic() < deadline:
                try:
                    job = queues[version].get_nowait()
                except queue.Empty:
                    break
                expected_id = job['scenario']['id'] + '-' + str(job['seed'])
                process.stdin.write(json.dumps(job, separators=(',', ':')) + '\n')
                process.stdin.flush()
                wait_until = min(deadline, time.monotonic() + 120)
                result = None
                while time.monotonic() < wait_until:
                    if process.poll() is not None:
                        raise RuntimeError(f'{version} worker exited {process.returncode}; see {logfile.name}')
                    if response.exists():
                        try:
                            candidate = json.loads(response.read_text())
                            if candidate.get('id') == expected_id:
                                result = candidate
                                break
                        except (ValueError, OSError):
                            pass
                    time.sleep(.02)
                if result is None:
                    if time.monotonic() >= deadline:
                        break
                    raise TimeoutError(expected_id)
                if result.get('error'):
                    raise RuntimeError(result['error'])
                result.update(versionLabel=version, map=job['scenario']['map'], tier=job['scenario']['tier'],
                              stage=job['scenario']['stage'], active=job['scenario']['active'])
                with lock:
                    rows.append(result)
                    with (out / 'runs.jsonl').open('a') as target:
                        target.write(json.dumps(result, ensure_ascii=False) + '\n')
                    if len(rows) % 40 == 0:
                        print(f'{len(rows)}/{total}; {time.monotonic()-started:.1f}s', flush=True)
        except Exception as error:
            with lock:
                errors.append(str(error))
        finally:
            if process.stdin:
                process.stdin.close()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()

    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        futures = [pool.submit(worker, version, index) for version in paths for index in range(args.workers // 2)]
        for future in futures:
            future.result()
    fields = ['map', 'tier', 'stage', 'active', 'version', 'runs', 'medianDepth', 'medianSeconds',
              'medianBlocks', 'medianValue', 'censored', 'trialRewards', 'failedTrials', 'trialSeconds',
              'trialWear', 'slowMiningSeconds']
    summary = []
    for map_id in range(3):
        for tier in range(5):
            for stage in range(2):
                for active in (False, True):
                    for version in paths:
                        group = [r for r in rows if (r['map'], r['tier'], r['stage'], r['active'], r['versionLabel']) == (map_id, tier, stage, active, version)]
                        if not group:
                            continue
                        row = dict(map=map_id, tier=tier, stage=stage, active=int(active), version=manifests[version]['version'], runs=len(group))
                        for source, label in [('depth', 'medianDepth'), ('seconds', 'medianSeconds'), ('blocks', 'medianBlocks'), ('value', 'medianValue')]:
                            row[label] = round(statistics.median(r[source] for r in group), 4)
                        for source, label in [('censored', 'censored'), ('trials', 'trialRewards'), ('failedTrials', 'failedTrials'), ('trialSeconds', 'trialSeconds'), ('trialWear', 'trialWear'), ('slowMiningSeconds', 'slowMiningSeconds')]:
                            row[label] = round(statistics.mean(r.get(source, 0) for r in group), 4) if source in group[0] else ''
                        summary.append(row)
    with (out / 'summary.csv').open('w') as target:
        writer = csv.DictWriter(target, fieldnames=fields)
        writer.writeheader()
        writer.writerows(summary)
    status = dict(completed=len(rows), total=total, seconds=round(time.monotonic()-started, 3), errors=errors,
                  complete=len(rows) == total and not errors)
    (out / 'status.json').write_text(json.dumps(status, ensure_ascii=False, indent=2))
    body = '<!doctype html><html lang="ru"><meta charset="utf-8"><title>PickaxeFall · сравнение баланса</title><style>body{font:16px system-ui;background:#182125;color:#eee;margin:24px}table{border-collapse:collapse}td,th{padding:8px;border:1px solid #586268}input{padding:10px;margin-bottom:16px}th{position:sticky;top:0;background:#26383c}</style><h1>PickaxeFall · {before} → {after}</h1>'.replace('{before}', html.escape(str(manifests['before']['version']))).replace('{after}', html.escape(str(manifests['after']['version'])))
    body += '<p>' + html.escape(json.dumps(status, ensure_ascii=False)) + '</p><p>Карты: 0 — шахта, 1 — пустыня, 2 — ад. Кирки: 0 — дерево … 4 — алмаз. Stage: 0 — без улучшений, 1 — четверть прокачки. Active: 0 — пассивно, 1 — бот. Censored — доля ветвей, дошедших до лимита 600 с; их результаты ограничены этим временем. Модель мини-игры — 4 нажатия/с, а не замер сложности для живого игрока.</p><input placeholder="Фильтр строк" oninput="for(const r of document.querySelectorAll(\'tbody tr\'))r.hidden=!r.innerText.toLowerCase().includes(this.value.toLowerCase())"><table><thead><tr>'
    body += ''.join('<th>' + field + '</th>' for field in fields) + '</tr></thead><tbody>'
    for row in summary:
        body += '<tr>' + ''.join('<td>' + html.escape(str(row.get(field, ''))) + '</td>' for field in fields) + '</tr>'
    body += '</tbody></table></html>'
    (out / 'report.html').write_text(body)
    print(json.dumps(status, ensure_ascii=False), flush=True)
    return 0 if status['complete'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
