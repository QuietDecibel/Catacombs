#ifndef CREATOR_H
#define CREATOR_H

#include <QMainWindow>
#include <QString>
#include <QFile>

namespace Ui {
class Creator;
}

class Creator : public QMainWindow
{
    Q_OBJECT

public:
    explicit Creator(QWidget *parent = nullptr);
    ~Creator();
    QString getQuestion();
    QString getAnswer();
    int getTimesClicked();

private slots:
    void on_QuestionButton_clicked();

private:
    Ui::Creator *ui;
    int timesClicked;
    int counter;
};

#endif // CREATOR_H
