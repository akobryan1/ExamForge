import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ExamService } from '../services/ExamService';
import { examKeys, gradingKeys, analyticsKeys, incidentKeys, submittedPapersKeys } from '../config/queryKeys';
import type { CreateExamFormData, CreateQuestionFormData } from '../types/exam';

// ── Queries (reads) ──

export function useExamList() {
  return useQuery({
    queryKey: examKeys.lists(),
    queryFn: () => ExamService.getExams(),
  });
}

export function useExamDetail(examId: string | undefined) {
  return useQuery({
    queryKey: examKeys.detail(examId!),
    queryFn: () => ExamService.getExamById(examId!),
    enabled: !!examId,
  });
}

export function useExamQuestions(examId: string | undefined) {
  return useQuery({
    queryKey: examKeys.questions(examId!),
    queryFn: () => ExamService.getExamQuestions(examId!),
    enabled: !!examId,
    staleTime: 0,
    gcTime: 0,
  });
}

export function useGradingQueue() {
  return useQuery({
    queryKey: gradingKeys.queue(),
    queryFn: () => ExamService.getGradingQueue(),
  });
}

export function useExamAnalytics(examId: string | undefined) {
  return useQuery({
    queryKey: analyticsKeys.exam(examId!),
    queryFn: () => ExamService.getExamAnalytics(examId!),
    enabled: !!examId,
  });
}

export function useIncidentReports() {
  return useQuery({
    queryKey: incidentKeys.list(),
    queryFn: () => ExamService.getIncidentReports(),
  });
}

export function useSubmittedPapers() {
  return useQuery({
    queryKey: submittedPapersKeys.list(),
    queryFn: () => ExamService.getSubmittedPapers(),
    refetchInterval: 30_000, // refresh every 30s as papers may be submitted
  });
}

// ── Mutations (writes with cache invalidation) ──

export function useCreateExam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateExamFormData) => ExamService.createExam(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: examKeys.lists() }),
  });
}

export function useUpdateExam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ examId, data }: { examId: string; data: Partial<CreateExamFormData> }) =>
      ExamService.updateExam(examId, data),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: examKeys.lists() });
      qc.invalidateQueries({ queryKey: examKeys.detail(vars.examId) });
    },
  });
}

export function useDeleteExam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (examId: string) => ExamService.deleteExam(examId),
    onMutate: async (examId) => {
      await qc.cancelQueries({ queryKey: examKeys.lists() });
      const previous = qc.getQueryData(examKeys.lists());
      qc.setQueryData(examKeys.lists(), (old: any) => {
        if (!old) return old;
        if (Array.isArray(old)) return old.filter((e: any) => e.id !== examId);
        return old;
      });
      return { previous };
    },
    onError: (err, examId, context) => {
      if (context?.previous) {
        qc.setQueryData(examKeys.lists(), context.previous);
      }
    },
    onSettled: () => qc.invalidateQueries({ queryKey: examKeys.lists(), refetchType: 'all' }),
  });
}

export function usePublishExam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (examId: string) => ExamService.publishExam(examId),
    onSuccess: (_, examId) => {
      qc.invalidateQueries({ queryKey: examKeys.lists() });
      qc.invalidateQueries({ queryKey: examKeys.detail(examId) });
    },
  });
}

export function useArchiveExam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (examId: string) => ExamService.archiveExam(examId),
    onSuccess: (_, examId) => {
      qc.invalidateQueries({ queryKey: examKeys.lists() });
      qc.invalidateQueries({ queryKey: examKeys.detail(examId) });
    },
  });
}

export function useCloneExam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (examId: string) => ExamService.cloneExam(examId),
    onSuccess: () => qc.invalidateQueries({ queryKey: examKeys.lists() }),
  });
}

export function useRepublishExam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (examId: string) => ExamService.republishExam(examId),
    onSuccess: () => qc.invalidateQueries({ queryKey: examKeys.lists() }),
  });
}

export function useCompleteExam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (examId: string) => ExamService.completeExam(examId),
    onSuccess: () => qc.invalidateQueries({ queryKey: examKeys.lists() }),
  });
}

export function useCreateQuestion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ examId, data }: { examId: string; data: CreateQuestionFormData }) =>
      ExamService.createQuestion(examId, data),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: examKeys.questions(vars.examId), refetchType: 'all' });
    },
  });
}

export function useUpdateQuestion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ questionId, examId, data }: { questionId: string; examId: string; data: Partial<CreateQuestionFormData> }) =>
      ExamService.updateQuestion(questionId, examId, data),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: examKeys.questions(vars.examId), refetchType: 'all' });
    },
  });
}

export function useDeleteQuestion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ questionId, examId }: { questionId: string; examId: string }) =>
      ExamService.deleteQuestion(questionId, examId),
    onMutate: async ({ questionId, examId }) => {
      // Cancel outgoing refetches so they don't overwrite our optimistic update
      await qc.cancelQueries({ queryKey: examKeys.questions(examId) });
      // Snapshot previous value
      const previous = qc.getQueryData(examKeys.questions(examId));
      // Optimistically remove the question from the cache
      qc.setQueryData(examKeys.questions(examId), (old: any[]) =>
        Array.isArray(old) ? old.filter((q: any) => q.id !== questionId) : old
      );
      return { previous };
    },
    onError: (err, vars, context) => {
      // Rollback on error
      if (context?.previous) {
        qc.setQueryData(examKeys.questions(vars.examId), context.previous);
      }
    },
    onSettled: (_, __, vars) => {
      qc.invalidateQueries({ queryKey: examKeys.questions(vars.examId), refetchType: 'all' });
    },
  });
}

export function useGradeQuestion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ attemptId, questionId, earnedPoints, feedback }: {
      attemptId: string; questionId: string; earnedPoints: number; feedback?: string
    }) => ExamService.gradeQuestion(attemptId, questionId, earnedPoints, feedback),
    onSuccess: () => qc.invalidateQueries({ queryKey: gradingKeys.all }),
  });
}

export function useArchiveIncidents() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (incidents: { id: string; studentId: string }[]) => ExamService.archiveIncidents(incidents),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKeys.all }),
  });
}

export function useUnarchiveIncidents() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (incidents: { id: string; studentId: string }[]) => ExamService.unarchiveIncidents(incidents),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKeys.all }),
  });
}

export function useDeleteIncidents() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (incidents: { id: string; studentId: string }[]) => ExamService.deleteIncidents(incidents),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKeys.all }),
  });
}
