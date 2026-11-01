import { useQuery } from '@tanstack/react-query';
import { useApi } from '../../api/context';

export function useMyCareerCentre() {
  const api = useApi();
  return useQuery({ queryKey: ['career', 'me', 'centre'], queryFn: () => api.getMyCareerCentre() });
}
