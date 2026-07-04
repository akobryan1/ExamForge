import jsPDF from 'jspdf';
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
// PDF Export (jsPDF — manual table, no autoTable dependency)
// ---------------------------------------------------------------------------

export function exportToPDF(
  data: Record<string, any>[],
  columns: ExportColumn[],
  title: string,
  filename: string,
) {
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });
  const pageW = doc.internal.pageSize.getWidth();
  const pageH = doc.internal.pageSize.getHeight();
  const marginLeft = 10;
  const marginRight = 10;
  const marginTop = 25;
  const usableW = pageW - marginLeft - marginRight;

  // Title
  doc.setFontSize(14);
  doc.text(title, marginLeft, 12);
  doc.setFontSize(8);
  doc.setTextColor(120);
  doc.text(`Generated: ${new Date().toLocaleString()}  •  ${data.length} rows`, marginLeft, 18);

  // Column widths (proportional)
  const colW = usableW / columns.length;

  // Table drawing helpers
  const rowH = 6;
  const headerH = 7;
  let y = marginTop;

  const drawHeader = () => {
    doc.setFillColor(49, 46, 129);
    doc.rect(marginLeft, y, usableW, headerH, 'F');
    doc.setTextColor(255);
    doc.setFontSize(7);
    doc.setFont('helvetica', 'bold');
    columns.forEach((col, i) => {
      const x = marginLeft + i * colW;
      doc.text(col.label, x + 0.5, y + 4.5);
    });
    y += headerH;
  };

  const drawRow = (row: Record<string, any>, rowIndex: number) => {
    // Alternate background
    if (rowIndex % 2 === 1) {
      doc.setFillColor(245, 245, 250);
      doc.rect(marginLeft, y, usableW, rowH, 'F');
    }
    doc.setTextColor(30);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(6.5);
    columns.forEach((col, i) => {
      const x = marginLeft + i * colW;
      doc.text(formatCellValue(row[col.key]).substring(0, Math.floor(colW / 1.5)), x + 0.5, y + 4);
    });
    // Bottom border
    doc.setDrawColor(220, 220, 220);
    doc.line(marginLeft, y + rowH, marginLeft + usableW, y + rowH);
    y += rowH;
  };

  drawHeader();

  data.forEach((row, i) => {
    // Check if we need a new page
    if (y + rowH > pageH - 15) {
      doc.setFontSize(8);
      doc.setTextColor(150);
      doc.text(`Continued...`, marginLeft, pageH - 8);
      doc.addPage();
      y = marginTop;
      drawHeader();
    }
    drawRow(row, i);
  });

  // Footer
  doc.setFontSize(7);
  doc.setTextColor(150);
  doc.text(`Page ${(doc as any).internal.getNumberOfPages?.() || 1}`, pageW / 2, pageH - 5, { align: 'center' });

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
