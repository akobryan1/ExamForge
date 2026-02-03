const express = require('express');
const { exec } = require('child_process');
const fs = require('fs').promises;
const path = require('path');

const app = express();
app.use(express.json({ limit: '50mb' }));

app.post('/api/publish', async (req, res) => {
  try {
    const { examId, htmlContent } = req.body;
    
    // Save HTML to public/exams folder
    const publicDir = path.join(__dirname, 'public', 'exams');
    await fs.mkdir(publicDir, { recursive: true });
    await fs.writeFile(path.join(publicDir, `${examId}.html`), htmlContent);
    
    // Deploy to Firebase Hosting
    exec('firebase deploy --only hosting', (error, stdout, stderr) => {
      if (error) {
        console.error('Deployment error:', error);
        return res.status(500).json({ error: 'Deployment failed', details: stderr });
      }
      
      res.json({ 
        success: true, 
        examUrl: `https://examforge-201e8.web.app/exams/${examId}.html`,
        examId 
      });
    });
  } catch (error) {
    res.status(500).json({ error: error.message });
  }
});

app.listen(3000, () => {
  console.log('Publishing server running on port 3000');
});