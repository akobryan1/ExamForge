import { QuestionType } from '../types/exam';

interface GenerateQuestionsRequest {
  material: string;
  questionType: QuestionType;
  count: number;
  difficulty?: 'easy' | 'medium' | 'hard';
  topic?: string;
}

interface GeneratedQuestion {
  type: QuestionType;
  text: string;
  description?: string;
  points: number;
  choices?: string[];
  correctAnswer: string | string[];
  explanation?: string;
}

export class AIQuestionGeneratorService {
  private static readonly OPENAI_API_KEY = process.env.OPENAI_API_KEY || 'sk-placeholder-key-replace-in-production';
  private static readonly API_ENDPOINT = 'https://api.openai.com/v1/chat/completions';

  /**
   * Generate questions from material using AI
   */
  static async generateQuestionsFromMaterial(
    request: GenerateQuestionsRequest
  ): Promise<GeneratedQuestion[]> {
    // If using placeholder key, return mock questions
    if (this.OPENAI_API_KEY === 'sk-placeholder-key-replace-in-production') {
      return this.generateMockQuestions(request);
    }

    try {
      const prompt = this.buildPrompt(request);
      
      const response = await fetch(this.API_ENDPOINT, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${this.OPENAI_API_KEY}`,
        },
        body: JSON.stringify({
          model: 'gpt-4',
          messages: [
            {
              role: 'system',
              content: 'You are an expert educator who creates high-quality exam questions. Always return valid JSON arrays of question objects.',
            },
            {
              role: 'user',
              content: prompt,
            },
          ],
          temperature: 0.7,
        }),
      });

      if (!response.ok) {
        throw new Error(`OpenAI API error: ${response.statusText}`);
      }

      const data: any = await response.json();
      const content = data.choices[0].message.content;
      
      // Parse JSON response
      const questions = JSON.parse(content);
      return questions;
    } catch (error) {
      console.error('AI question generation failed:', error);
      // Fallback to mock questions
      return this.generateMockQuestions(request);
    }
  }

  /**
   * Build prompt for AI question generation
   */
  private static buildPrompt(request: GenerateQuestionsRequest): string {
    const { material, questionType, count, difficulty = 'medium', topic } = request;

    let prompt = `Generate ${count} ${difficulty} difficulty ${questionType.replace('_', ' ')} questions`;
    
    if (topic) {
      prompt += ` about ${topic}`;
    }
    
    prompt += ` based on the following material:\n\n${material}\n\n`;
    prompt += `Return a JSON array of question objects with this structure:\n`;

    switch (questionType) {
      case 'multiple_choice':
        prompt += `[{
  "type": "multiple_choice",
  "text": "Question text here?",
  "description": "Optional additional context",
  "points": 1,
  "choices": ["Option A", "Option B", "Option C", "Option D"],
  "correctAnswer": "Option A",
  "explanation": "Why this is correct"
}]`;
        break;
      
      case 'true_false':
        prompt += `[{
  "type": "true_false",
  "text": "Statement to evaluate",
  "points": 1,
  "correctAnswer": "true" or "false",
  "explanation": "Why this is true/false"
}]`;
        break;

      case 'modified_true_false':
        prompt += `[{
  "type": "modified_true_false",
  "text": "Statement to evaluate",
  "points": 2,
  "correctAnswer": "false. The correct answer is: [correction text]",
  "explanation": "Explanation of the correction"
}]`;
        break;

      case 'essay':
        prompt += `[{
  "type": "essay",
  "text": "Essay prompt",
  "description": "What to include in the answer",
  "points": 10,
  "correctAnswer": "Key points that should be covered"
}]`;
        break;

      case 'identification':
        prompt += `[{
  "type": "identification",
  "text": "What is being asked to identify?",
  "points": 1,
  "correctAnswer": "The expected answer"
}]`;
        break;

      case 'enumeration':
        prompt += `[{
  "type": "enumeration",
  "text": "List/enumerate what is being asked",
  "points": 3,
  "correctAnswer": ["Item 1", "Item 2", "Item 3"],
  "explanation": "Explanation of the items"
}]`;
        break;
    }

    return prompt;
  }

  /**
   * Generate mock questions for testing (when using placeholder API key)
   */
  private static generateMockQuestions(request: GenerateQuestionsRequest): GeneratedQuestion[] {
    const { questionType, count } = request;
    const questions: GeneratedQuestion[] = [];

    for (let i = 0; i < count; i++) {
      switch (questionType) {
        case 'multiple_choice':
          questions.push({
            type: questionType,
            text: `Sample Multiple Choice Question ${i + 1}?`,
            description: 'This is a mock question generated for testing',
            points: 1,
            choices: ['Option A', 'Option B', 'Option C', 'Option D'],
            correctAnswer: 'Option A',
            explanation: 'This is the mock correct answer',
          });
          break;

        case 'true_false':
          questions.push({
            type: questionType,
            text: `Sample True/False Statement ${i + 1}`,
            points: 1,
            correctAnswer: 'true',
            explanation: 'This is a mock true/false question',
          });
          break;

        case 'modified_true_false':
          questions.push({
            type: questionType,
            text: `Sample Modified True/False Statement ${i + 1}`,
            points: 2,
            correctAnswer: 'false. The correct answer is: Sample correction text',
            explanation: 'This is a mock modified true/false question',
          });
          break;

        case 'essay':
          questions.push({
            type: questionType,
            text: `Sample Essay Question ${i + 1}`,
            description: 'Discuss the topic in detail',
            points: 10,
            correctAnswer: 'Key points: 1) Point one 2) Point two 3) Point three',
          });
          break;

        case 'identification':
          questions.push({
            type: questionType,
            text: `Identify: Sample Question ${i + 1}`,
            points: 1,
            correctAnswer: 'Sample Answer',
          });
          break;

        case 'enumeration':
          questions.push({
            type: questionType,
            text: `Enumerate: Sample Question ${i + 1}`,
            points: 3,
            correctAnswer: ['Item 1', 'Item 2', 'Item 3'],
            explanation: 'These are the three items',
          });
          break;
      }
    }

    return questions;
  }

  /**
   * Extract text from PDF (would require pdf-parse in production)
   */
  static async extractTextFromPDF(fileBuffer: Buffer): Promise<string> {
    // Placeholder - in production, use pdf-parse
    // const pdf = require('pdf-parse');
    // const data = await pdf(fileBuffer);
    // return data.text;
    
    return '[PDF text extraction requires pdf-parse package. Install with: npm install pdf-parse]';
  }
}
