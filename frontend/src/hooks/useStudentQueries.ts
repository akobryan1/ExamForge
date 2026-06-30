import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { StudentService } from '../services/StudentService';
import { studentKeys } from '../config/queryKeys';

export function useStudents(instructorId: string | undefined) {
  return useQuery({
    queryKey: studentKeys.list(),
    queryFn: () => StudentService.getStudents(instructorId!),
    enabled: !!instructorId,
  });
}

export function useRegistrationFields(instructorId: string | undefined) {
  return useQuery({
    queryKey: [...studentKeys.list(), 'fields', instructorId],
    queryFn: () => StudentService.getRegistrationFields(instructorId!),
    enabled: !!instructorId,
  });
}

export function useSaveRegistrationFields() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ instructorId, fields }: { instructorId: string; fields: any }) =>
      StudentService.saveRegistrationFields(instructorId, fields),
    onSuccess: () => qc.invalidateQueries({ queryKey: studentKeys.all }),
  });
}
