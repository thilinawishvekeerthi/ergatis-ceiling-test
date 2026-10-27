import { labels } from '../labels';

/** Placeholder screens for portals whose backend modules do not exist yet. */
export function Placeholder({ title }: { title: string }) {
  return (
    <section>
      <h2>{title}</h2>
      <p>This screen arrives with its backend module.</p>
    </section>
  );
}

export const CareerCentre = () => <Placeholder title={labels.nav.career} />;
export const Assessments = () => <Placeholder title={labels.nav.assessments} />;
export const TeamIdps = () => <Placeholder title={labels.nav.teamIdps} />;
