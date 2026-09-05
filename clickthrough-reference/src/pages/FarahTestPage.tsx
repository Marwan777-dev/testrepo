import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { CheckCircle } from "lucide-react";

export default function FarahTestPage() {
  return (
    <div className="max-w-3xl mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
      <h1 className="text-2xl font-heading font-bold">Farah's Test Page</h1>
      <Card>
        <CardHeader> 
          <CardTitle className="flex items-center gap-2">
            <CheckCircle className="size-5 text-d2" />
            BA Workflow Test
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            This verifies the full flow: push → auto PR → Vercel preview.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
