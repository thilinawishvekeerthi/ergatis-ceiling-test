import { useConfigs } from '../../api/hooks';
import { labels } from '../../labels';
import { ConfigList } from './ConfigList';

export function ScalesList() {
  const q = useConfigs('Scale');
  return (
    <section>
      <h2>{labels.nav.scales}</h2>
      <ConfigList items={q.data} isLoading={q.isLoading} isError={q.isError} empty={labels.emptyScales} basePath="/admin/scales" />
    </section>
  );
}
