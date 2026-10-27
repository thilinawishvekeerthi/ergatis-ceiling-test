import { useConfigs } from '../../api/hooks';
import { labels } from '../../labels';
import { ConfigList } from './ConfigList';

export function GridsList() {
  const q = useConfigs('Grid');
  return (
    <section>
      <h2>{labels.nav.grids}</h2>
      <ConfigList items={q.data} isLoading={q.isLoading} isError={q.isError} empty={labels.emptyGrids} basePath="/admin/grids" />
    </section>
  );
}
