#ifndef STARTWINDOW_H
#define STARTWINDOW_H

#include <QMainWindow>
#include "Questions.h"
#include "Dungeonspawner.h"
#include <QList>

namespace Ui {
class startwindow;
}

class startwindow : public QMainWindow
{
    Q_OBJECT

public:
    explicit startwindow(QWidget *parent = nullptr);
    ~startwindow();
    QList<Questions *> QuestList;
    int i = 0;
    DungeonSpawner *DMSGame;

private slots:

    void on_StartButton_clicked();

    void on_QuitButton_clicked();

    void on_QuestionsBox_textChanged();

    void on_PointAwards_overflow();

    void on_NextQButton_clicked();

    void on_QuitButton_2_clicked();

    void on_PlayGame_clicked();

    void on_pushButton_clicked();

    void on_Profile_2_clicked();

    void on_PlayButton_clicked();

    void on_CreatorWindowButton_clicked();

    void on_StatPageButton_clicked();

    void on_Q_2_Pofile_clicked();

    void on_GS_Creator_clicked();

    void on_GS_Start_clicked();

    void on_GS_Profile_clicked();

    void on_PR_Creator_clicked();

    void on_PR_Play_clicked();

    void on_PR_Profile_clicked();

    void on_CR_PlayScreen_clicked();

private:
    Ui::startwindow *ui;
};

#endif // STARTWINDOW_H
