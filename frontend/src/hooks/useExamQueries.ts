import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ExamService } from '../services/ExamService';
import { examKeys, gradingKeys, analyticsKeys, incidentKeys } from '../config/queryKeys';
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
    onSuccess: () => qc.invalidateQueries({ queryKey: examKeys.lists() }),
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

export function useCreateQuestion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ examId, data }: { examId: string; data: CreateQuestionFormData }) =>
      ExamService.createQuestion(examId, data),
    onSuccess: (_, vars) => qc.invalidateQueries({ queryKey: examKeys.questions(vars.examId) }),
  });
}

export function useUpdateQuestion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ questionId, examId, data }: { questionId: string; examId: string; data: Partial<CreateQuestionFormData> }) =>
      ExamService.updateQuestion(questionId, examId, data),
    onSuccess: (_, vars) => qc.invalidateQueries({ queryKey: examKeys.questions(vars.examId) }),
  });
}

export function useDeleteQuestion() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ questionId, examId }: { questionId: string; examId: string }) =>
      ExamService.deleteQuestion(questionId, examId),
    onSuccess: (_, vars) => qc.invalidateQueries({ queryKey: examKeys.questions(vars.examId) }),
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
    mutationFn: (ids: string[]) => ExamService.archiveIncidents(ids),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKeys.all }),
  });
}

export function useUnarchiveIncidents() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (ids: string[]) => ExamService.unarchiveIncidents(ids),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKeys.all }),
  });
}

export function useDeleteIncidents() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (ids: string[]) => ExamService.deleteIncidents(ids),
    onSuccess: () => qc.invalidateQueries({ queryKey: incidentKeys.all }),
  });
}
