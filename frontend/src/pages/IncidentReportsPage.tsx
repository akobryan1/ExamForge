import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { ExamService } from '../services/ExamService';
import { Button } from '../components/Button';
import { pageTransition } from '../utils/animations';
import '../styles/pages/incident-reports.css';

interface IncidentReport {
  id: string;
  studentId: string;
  studentName: string;
  examId: string;
  examTitle: string;
  eventType: string;
  eventDetail: string;
  timestamp: Date;
  severity: 'low' | 'medium' | 'high';
  archived: boolean;
}

export function IncidentReportsPage() {
  const [incidents, setIncidents] = useState<IncidentReport[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  
  const [filterStatus, setFilterStatus] = useState<'all' | 'active' | 'archived'>('active');
  const [filterSeverity, setFilterSeverity] = useState<'all' | 'low' | 'medium' | 'high'>('all');
  const [searchTerm, setSearchTerm] = useState('');
  
  const [selectedIncidents, setSelectedIncidents] = useState<Set<string>>(new Set());

  useEffect(() => {
    loadIncidents();
  }, []);

  const loadIncidents = async () => {
    try {
      setLoading(true);
      setError('');
      const data = await ExamService.getIncidentReports();
      setIncidents(data);
    } catch (err: any) {
      // Incidents endpoint may not be available yet
      setIncidents([]);
    } finally {
      setLoading(false);
    }
  };

  const filteredIncidents = incidents.filter(incident => {
    // Status filter
    if (filterStatus === 'active' && incident.archived) return false;
    if (filterStatus === 'archived' && !incident.archived) return false;
    
    // Severity filter
    if (filterSeverity !== 'all' && incident.severity !== filterSeverity) return false;
    
    // Search filter
    if (searchTerm) {
      const search = searchTerm.toLowerCase();
      return (
        incident.studentName.toLowerCase().includes(search) ||
        incident.studentId.toLowerCase().includes(search) ||
        incident.examTitle.toLowerCase().includes(search) ||
        incident.eventType.toLowerCase().includes(search)
      );
    }
    
    return true;
  });

  const toggleIncidentSelection = (id: string) => {
    setSelectedIncidents(prev => {
      const newSet = new Set(prev);
      if (newSet.has(id)) {
        newSet.delete(id);
      } else {
        newSet.add(id);
      }
      return newSet;
    });
  };

  const toggleSelectAll = () => {
    if (selectedIncidents.size === filteredIncidents.length) {
      setSelectedIncidents(new Set());
    } else {
      setSelectedIncidents(new Set(filteredIncidents.map(i => i.id)));
    }
  };

  const handleArchiveSelected = async () => {
    if (selectedIncidents.size === 0) return;
    
    try {
      await ExamService.archiveIncidents(Array.from(selectedIncidents));
      setSelectedIncidents(new Set());
      await loadIncidents();
    } catch (err: any) {
      alert('Failed to archive incidents: ' + err.message);
    }
  };

  const handleUnarchiveSelected = async () => {
    if (selectedIncidents.size === 0) return;
    
    try {
      await ExamService.unarchiveIncidents(Array.from(selectedIncidents));
      setSelectedIncidents(new Set());
      await loadIncidents();
    } catch (err: any) {
      alert('Failed to unarchive incidents: ' + err.message);
    }
  };

  const handleDeleteSelected = async () => {
    if (selectedIncidents.size === 0) return;
    
    if (!confirm(`Are you sure you want to delete ${selectedIncidents.size} incident(s)? This cannot be undone.`)) {
      return;
    }
    
    try {
      await ExamService.deleteIncidents(Array.from(selectedIncidents));
      setSelectedIncidents(new Set());
      await loadIncidents();
    } catch (err: any) {
      alert('Failed to delete incidents: ' + err.message);
    }
  };

  const getSeverityColor = (severity: string) => {
    switch (severity) {
      case 'high': return '#dc2626';
      case 'medium': return '#f59e0b';
      case 'low': return '#10b981';
      default: return '#6b7280';
    }
  };

  if (loading) {
    return (
      <MainLayout>
        <div style={{ textAlign: 'center', padding: 'var(--spacing-12)' }}>
          <p>Loading incident reports...</p>
        </div>
      </MainLayout>
    );
  }

  return (
    <MainLayout>
      <motion.div
        className="incident-reports-page"
        variants={pageTransition}
        initial="initial"
        animate="animate"
        exit="exit"
      >
        <div className="page-header">
          <div>
            <h1>Incident Reports</h1>
            <p className="page-subtitle">
              Monitor and manage exam integrity violations
            </p>
          </div>
        </div>

        {error && (
          <div className="error-banner">{error}</div>
        )}

        {/* Filters and Actions */}
        <div className="filters-section">
          <div className="filters">
            <div className="filter-group">
              <label>Status</label>
              <select value={filterStatus} onChange={(e) => setFilterStatus(e.target.value as any)}>
                <option value="all">All</option>
                <option value="active">Active</option>
                <option value="archived">Archived</option>
              </select>
            </div>

            <div className="filter-group">
              <label>Severity</label>
              <select value={filterSeverity} onChange={(e) => setFilterSeverity(e.target.value as any)}>
                <option value="all">All Severities</option>
                <option value="low">Low</option>
                <option value="medium">Medium</option>
                <option value="high">High</option>
              </select>
            </div>

            <div className="filter-group search-group">
              <label>Search</label>
              <input
                type="text"
                placeholder="Search by student, exam, or type..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
          </div>

          {selectedIncidents.size > 0 && (
            <div className="bulk-actions">
              <span className="selection-count">{selectedIncidents.size} selected</span>
              <Button variant="outline" size="sm" onClick={handleArchiveSelected}>
                Archive
              </Button>
              <Button variant="outline" size="sm" onClick={handleUnarchiveSelected}>
                Unarchive
              </Button>
              <Button variant="outline" size="sm" onClick={handleDeleteSelected}>
                Delete
              </Button>
            </div>
          )}
        </div>

        {/* Incidents Table */}
        {filteredIncidents.length === 0 ? (
          <div className="empty-state">
            <h3>No incidents found</h3>
            <p>
              {searchTerm || filterStatus !== 'all' || filterSeverity !== 'all'
                ? 'Try adjusting your filters'
                : 'No exam integrity violations have been recorded'}
            </p>
          </div>
        ) : (
          <div className="incidents-table-container">
            <table className="incidents-table">
              <thead>
                <tr>
                  <th>
                    <input
                      type="checkbox"
                      checked={selectedIncidents.size === filteredIncidents.length}
                      onChange={toggleSelectAll}
                    />
                  </th>
                  <th>Student</th>
                  <th>Student ID</th>
                  <th>Exam</th>
                  <th>Violation Type</th>
                  <th>Details</th>
                  <th>Severity</th>
                  <th>Timestamp</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {filteredIncidents.map(incident => (
                  <tr key={incident.id} className={selectedIncidents.has(incident.id) ? 'selected' : ''}>
                    <td>
                      <input
                        type="checkbox"
                        checked={selectedIncidents.has(incident.id)}
                        onChange={() => toggleIncidentSelection(incident.id)}
                      />
                    </td>
                    <td className="student-name">{incident.studentName}</td>
                    <td className="student-id">{incident.studentId}</td>
                    <td className="exam-title">{incident.examTitle}</td>
                    <td className="event-type">{incident.eventType.replace('_', ' ')}</td>
                    <td className="event-detail">{incident.eventDetail}</td>
                    <td>
                      <span 
                        className="severity-badge"
                        style={{ backgroundColor: getSeverityColor(incident.severity) }}
                      >
                        {incident.severity}
                      </span>
                    </td>
                    <td className="timestamp">
                      {new Date(incident.timestamp).toLocaleString()}
                    </td>
                    <td>
                      <span className={`status-badge ${incident.archived ? 'archived' : 'active'}`}>
                        {incident.archived ? 'Archived' : 'Active'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="incidents-summary">
          <p>
            Showing {filteredIncidents.length} of {incidents.length} incident{incidents.length !== 1 ? 's' : ''}
          </p>
        </div>
      </motion.div>
    </MainLayout>
  );
}
