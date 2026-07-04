import jsPDF from 'jspdf';
import 'jspdf-autotable';
import Papa from 'papaparse';

// ---------------------------------------------------------------------------
// Column definitions for the ExportPanel
// ---------------------------------------------------------------------------

export interface ExportColumn {
  key: string;
  label: string;
}

export const PAPER_COLUMNS: ExportColumn[] = [
  { key: 'studentName', label: 'Student Name' },
  { key: 'studentNumber', label: 'Student ID' },
  { key: 'studentEmail', label: 'Email' },
  { key: 'studentSection', label: 'Section' },
  { key: 'studentCourse', label: 'Course' },
  { key: 'studentYear', label: 'Year' },
  { key: 'examTitle', label: 'Exam Title' },
  { key: 'score', label: 'Score' },
  { key: 'totalPoints', label: 'Total Points' },
  { key: 'percentage', label: 'Percentage' },
  { key: 'passed', label: 'Passed' },
  { key: 'status', label: 'Status' },
  { key: 'timeSpent', label: 'Time Spent' },
  { key: 'submittedAt', label: 'Submitted' },
  { key: 'hasIncidents', label: 'Has Incidents' },
];

export const INCIDENT_COLUMNS: ExportColumn[] = [
  { key: 'studentName', label: 'Student Name' },
  { key: 'studentNumber', label: 'Student ID' },
  { key: 'examTitle', label: 'Exam Title' },
  { key: 'eventType', label: 'Violation Type' },
  { key: 'eventDetail', label: 'Details' },
  { key: 'severity', label: 'Severity' },
  { key: 'timestamp', label: 'Timestamp' },
  { key: 'archived', label: 'Status' },
];

// ---------------------------------------------------------------------------
// Presets
// ---------------------------------------------------------------------------

export interface Preset {
  name: string;
  keys: string[];
}

export const PAPER_PRESETS: Preset[] = [
  { name: 'Grades Only', keys: ['studentName', 'studentNumber', 'examTitle', 'score', 'percentage', 'status'] },
  { name: 'Full Report', keys: ['studentName', 'studentNumber', 'studentEmail', 'studentSection', 'examTitle', 'score', 'totalPoints', 'percentage', 'passed', 'status', 'timeSpent', 'submittedAt'] },
  { name: 'Grades + Incidents', keys: ['studentName', 'studentNumber', 'examTitle', 'score', 'percentage', 'status', 'hasIncidents'] },
];

export const INCIDENT_PRESETS: Preset[] = [
  { name: 'Summary', keys: ['studentName', 'studentNumber', 'examTitle', 'eventType', 'severity', 'archived'] },
  { name: 'Full Details', keys: ['studentName', 'studentNumber', 'examTitle', 'eventType', 'eventDetail', 'severity', 'timestamp', 'archived'] },
];

// ---------------------------------------------------------------------------
// CSV Export (PapaParse)
// ---------------------------------------------------------------------------

export function exportToCSV(
  data: Record<string, any>[],
  columns: ExportColumn[],
  filename: string,
) {
  const headers = columns.map(c => c.label);
  const rows = data.map(row => columns.map(c => formatCellValue(row[c.key])));

  const csv = Papa.unparse({ fields: headers, data: rows });
  downloadBlob(csv, `${filename}.csv`, 'text/csv;charset=utf-8;');
}

// ---------------------------------------------------------------------------
// PDF Export (jsPDF + autoTable)
// ---------------------------------------------------------------------------

export function exportToPDF(
  data: Record<string, any>[],
  columns: ExportColumn[],
  title: string,
  filename: string,
) {
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });

  // Title
  doc.setFontSize(14);
  doc.text(title, 14, 15);
  doc.setFontSize(9);
  doc.setTextColor(120);
  doc.text(`Generated: ${new Date().toLocaleString()}  •  ${data.length} rows`, 14, 22);

  // Table
  const headers = columns.map(c => c.label);
  const rows = data.map(row => columns.map(c => formatCellValue(row[c.key])));

  (doc as any).autoTable({
    head: [headers],
    body: rows,
    startY: 28,
    styles: {
      fontSize: 8,
      cellPadding: 2,
      lineColor: [200, 200, 200],
      lineWidth: 0.1,
    },
    headStyles: {
      fillColor: [49, 46, 129],
      textColor: 255,
      fontStyle: 'bold',
    },
    alternateRowStyles: {
      fillColor: [245, 245, 250],
    },
  });

  doc.save(`${filename}.pdf`);
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function formatCellValue(value: any): string {
  if (value === null || value === undefined) return '';
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (value instanceof Date || (typeof value === 'string' && !isNaN(Date.parse(value)))) {
    const d = new Date(value);
    if (!isNaN(d.getTime())) return d.toLocaleDateString();
  }
  if (typeof value === 'object') {
    if (value.toDate) return formatCellValue(value.toDate());
    if (value._seconds) return new Date(value._seconds * 1000).toLocaleDateString();
  }
  return String(value);
}

function downloadBlob(content: string, filename: string, mime: string) {
  const blob = new Blob(['\uFEFF' + content], { type: mime }); // BOM for Excel
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
