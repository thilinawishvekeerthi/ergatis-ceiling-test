import { useEffect, useId, useRef } from 'react';
import * as echarts from 'echarts/core';
import { BarChart } from 'echarts/charts';
import { GridComponent, TooltipComponent } from 'echarts/components';
import { SVGRenderer } from 'echarts/renderers';
import type { ScoreBreakdownData } from '../api/types';

echarts.use([BarChart, GridComponent, TooltipComponent, SVGRenderer]);

/** Weighted contribution of a component to the total. */
export function contribution(weight: number, score: number): number {
  return Math.round(weight * score * 100) / 100;
}

function Chart({ data }: { data: ScoreBreakdownData }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const chart = echarts.init(el, undefined, { renderer: 'svg', width: 360, height: 28 * data.components.length + 40 });
    chart.setOption({
      animation: false,
      grid: { left: 110, right: 16, top: 8, bottom: 24 },
      tooltip: { trigger: 'item' },
      xAxis: { type: 'value', max: data.max },
      yAxis: { type: 'category', inverse: true, data: data.components.map((c) => c.label) },
      series: [{ type: 'bar', data: data.components.map((c) => contribution(c.weight, c.score)), itemStyle: { color: '#2f5d62' } }],
    });
    return () => chart.dispose();
  }, [data]);
  return <div ref={ref} aria-hidden="true" />;
}

/**
 * Shows how a total (% match, bench strength, IDP progress) was built.
 * The table is the accessible source of truth; the chart is a visual aid.
 */
export function ScoreBreakdown({ data }: { data: ScoreBreakdownData }) {
  const titleId = useId();
  return (
    <section aria-labelledby={titleId}>
      <h3 id={titleId}>{data.title}</h3>
      <p>
        <strong>{data.total}</strong> out of {data.max}
      </p>
      <Chart data={data} />
      <table>
        <thead>
          <tr>
            <th scope="col">Component</th>
            <th scope="col">Weight</th>
            <th scope="col">Score</th>
            <th scope="col">Adds</th>
          </tr>
        </thead>
        <tbody>
          {data.components.map((c) => (
            <tr key={c.key}>
              <th scope="row">
                {c.label}
                {c.note && <small style={{ display: 'block', fontWeight: 400 }}>{c.note}</small>}
              </th>
              <td>{Math.round(c.weight * 100)}%</td>
              <td>{c.score}</td>
              <td>{contribution(c.weight, c.score)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
